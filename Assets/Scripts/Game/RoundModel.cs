using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class RoundModel : NetworkBehaviour
{
    private enum RoundStage
    {
        PREPARATION,
        GAME,
        ENDING
    }

    private QueueControlBehavior queueControlBehavior => GameManager.Instance.GetComponent<QueueControlBehavior>();
    private DeckControlBehavior deckControlBehavior => GameManager.Instance.GetComponent<DeckControlBehavior>();
    private BettingControlBehavior bettingController => GameManager.Instance.GetComponent<BettingControlBehavior>();

    private List<IPlayerController> playerModels = new List<IPlayerController>();
    private RoundStage roundStage = RoundStage.PREPARATION;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[RoundModel] OnNetworkSpawn | ObjId={NetworkObjectId} | IsServer={IsServer} | IsOwner={IsOwner}");
    }

    private void Awake()
    {
        UpdatePlayers();
    }

    public int GetCurrentHighestBet() => bettingController.GetCurrentHighestBet();

    public void StartGame(IPlayerController[] players, IPlayerController firstPlayer)
    {
        queueControlBehavior.SetFirstPlayerToMove(firstPlayer);
        queueControlBehavior.SetPlayers(players.ToList());

        StartRound();
    }

    public void StartRound()
    {
        roundStage = RoundStage.GAME;
        deckControlBehavior.DealCards(playerModels);

        bettingController.InitializeBetting(playerModels);

        if (IsServer)
        {
            SendRoundStartMessage(playerModels[0].PlayerId);
        }

        queueControlBehavior.StartRound();
    }

    public void StartGame(ulong[] playerIds, ulong firstPlayerId)
    {
        roundStage = RoundStage.GAME;
        UpdatePlayers();

        var orderedPlayerModels = new List<IPlayerController>();
        foreach (var id in playerIds)
        {
            var pm = playerModels.FirstOrDefault(p => p.PlayerId == id);
            if (pm == null)
            {
                var netObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(id);
                if (netObj != null)
                    pm = netObj.GetComponents<MonoBehaviour>().OfType<IPlayerController>().FirstOrDefault();
            }
            if (pm != null)
                orderedPlayerModels.Add(pm);
        }

        deckControlBehavior.DealCards(orderedPlayerModels);

        IPlayerController firstPlayer = orderedPlayerModels.FirstOrDefault(p => p.PlayerId == firstPlayerId)
            ?? NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(firstPlayerId)?
                .GetComponents<MonoBehaviour>()
                .OfType<IPlayerController>()
                .FirstOrDefault();

        queueControlBehavior.SetPlayers(orderedPlayerModels);
        queueControlBehavior.SetFirstPlayerToMove(firstPlayer);

        StartRound();
    }

    private void UpdatePlayers()
    {
        foreach (var player in FindObjectsOfType<MonoBehaviour>().OfType<IPlayerController>())
        {
            if (!playerModels.Contains(player))
            {
                playerModels.Add(player);
                Debug.Log("New player added: " + (player as MonoBehaviour)?.name);
            }
        }
    }

    public void RemovePlayerById(ulong playerId)
    {
        var pm = playerModels.FirstOrDefault(p => p.PlayerId == playerId);
        if (pm != null)
        {
            playerModels.Remove(pm);
            Debug.Log($"[RoundModel] Removed player {playerId} from RoundModel.playerModels");
        }
        else
        {
            Debug.LogWarning($"[RoundModel] RemovePlayerById: player {playerId} not found in playerModels");
        }


        try
        {
            bettingController?.RemovePlayerById(playerId);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RoundModel] Error removing player from BettingControlBehavior: {ex.Message}");
        }

        try
        {
            queueControlBehavior?.RemovePlayerById(playerId);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RoundModel] Error removing player from QueueControlBehavior: {ex.Message}");
        }

        if (IsServer)
        {
            UpdatePlayersState();
        }
    }

    public void EndRound()
    {
        if (roundStage != RoundStage.GAME) return;
        roundStage = RoundStage.ENDING;
        Debug.Log("Round ended");

        if (IsServer)
        {
            SendRoundEndMessage();
        }

        StartCoroutine(Delay(2, () =>
        {
            List<CardModel> tableCards = deckControlBehavior.GetCardsOnTable();
            List<(ulong, List<CardModel>)> playerHands = playerModels
                .Select(model => (model.PlayerId, model.CardsInHand))
                .Where(p => !bettingController.HasPlayerFolded(p.Item1))
                .ToList();

            List<ulong> winners = new List<ulong>();
            if (playerHands.Count > 1)
            {
                winners = HandEvaluator.GetWinners(playerHands, tableCards);
                
            }
            else if (playerHands.Count == 1)
            {
                winners.Add(playerHands[0].Item1);
            }

            
            int potAmount = bettingController.GetCurrentBank();

            if (IsServer)
            {
                SendWinnerMessages(winners, potAmount);
            }

            if (winners.Count > 0)
            {
                int winnerAmount = potAmount / winners.Count;
                winners.ForEach(winner =>
                {
                    bettingController.DistributeWinnings(winner, winnerAmount);
                });
            }

            if (IsServer)
            {
                var playersToKick = new List<IPlayerController>();
                foreach (var pm2 in playerModels)
                {
                    try
                    {
                        int balance = bettingController.GetPlayerBalance(pm2.PlayerId);
                        if (balance <= 0)
                        {
                            playersToKick.Add(pm2);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to check balance for player {pm2.PlayerId}: {ex.Message}");
                    }
                }

                foreach (var p in playersToKick)
                {
                    try
                    {
                        Debug.Log($"Kicking player {p.PlayerId} due to zero balance after round.");
                        p.Kick();
                        RemovePlayerById(p.PlayerId);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to kick/remove player {p.PlayerId}: {ex.Message}");
                    }
                }
            }

            Debug.Log("Round cleanup started");
            deckControlBehavior.CollectAllCards();
            roundStage = RoundStage.PREPARATION;

            StartCoroutine(Delay(2, () =>
            {
                roundStage = RoundStage.GAME;
                deckControlBehavior.DealCards(playerModels);

                bettingController.InitializeBetting(playerModels);

                if (IsServer)
                {
                    SendNewRoundMessage();
                }

                StartRound();
            }));
        }));
    }

    private IEnumerator Delay(int seconds, Action code)
    {
        yield return new WaitForSeconds(seconds);
        code();
    }

    public void TryToMakePlayerAction(IPlayerAction action)
    {
        Debug.Log($"[RoundModel] TryToMakePlayerAction | IsSpawned={IsSpawned} | IsServer={IsServer}");

        if (IsServer)
        {
            SendPlayerActionMessage(action.Player.PlayerId, action);
            ProcessPlayerAction(action.Player.PlayerId, action);
        }
        else
        {
            var wrapper = new NetworkPlayerAction
            {
                ActionType = action.TypeId,
                BetAmount = action.NewBet
            };
            SubmitPlayerActionServerRpc(wrapper);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitPlayerActionServerRpc(NetworkPlayerAction networkAction, ServerRpcParams rpcParams = default)
    {
        if (roundStage != RoundStage.GAME) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        IPlayerAction action = networkAction.ToAction();

        var netObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(senderId);
        if (netObj != null)
        {
            var actor = netObj.GetComponents<MonoBehaviour>().OfType<IPlayerController>().FirstOrDefault();
            if (actor != null)
            {
                action.Player = actor;
            }
        }

        Debug.Log($"[SERVER] Received action type {networkAction.ActionType} with bet {networkAction.BetAmount} from {senderId}");

        SendPlayerActionMessage(senderId, action);
        ProcessPlayerAction(senderId, action);

        NotifyPlayerActionClientRpc(senderId, networkAction);
    }

    [ClientRpc]
    private void NotifyPlayerActionClientRpc(ulong playerId, NetworkPlayerAction networkAction)
    {
        Debug.Log($"[CLIENT] Player {playerId} made action={networkAction.ActionType}, bet={networkAction.BetAmount}");

        if (!IsServer)
        {
            IPlayerAction action = networkAction.ToAction();

            var netObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(playerId);
            if (netObj != null)
            {
                var actor = netObj.GetComponents<MonoBehaviour>().OfType<IPlayerController>().FirstOrDefault();
                if (actor != null)
                {
                    action.Player = actor;
                }
            }

            ProcessPlayerAction(playerId, action);
        }
    }

    private void ProcessPlayerAction(ulong playerId, IPlayerAction action)
    {
        if (roundStage != RoundStage.GAME) return;

        Debug.Log($"[RoundModel] Processing {action.GetType().Name} for player {playerId}");

        IPlayerController player = action.Player;

        if (!queueControlBehavior.ShouldAcceptActionFromPlayer(player)) return;

        bool actionSuccessful = bettingController.ProcessPlayerAction(playerId, action, player);

        if (actionSuccessful)
        {
            Debug.Log($"Current bank is {bettingController.GetCurrentBank()}");
            queueControlBehavior.SwitchTurnToNextPlayer();
            UpdatePlayersState();

            ulong nextPlayerId = queueControlBehavior.GetCurrentPlayerId();
            if (nextPlayerId != playerId && IsServer)
            {
                SendTurnChangeMessage(nextPlayerId);
            }
        }
    }

    private void UpdatePlayersState()
    {
        if (!IsServer) return;

        foreach (var player in playerModels)
        {
            bettingController.UpdatePlayerState(player);
        }
    }

    private void SendRoundStartMessage(ulong firstPlayerId)
    {
        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.SendRoundStartServerRpc(firstPlayerId);
        }
    }

    private void SendRoundEndMessage()
    {
        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.SendRoundEndServerRpc();
        }
    }

    private void SendWinnerMessages(List<ulong> winners, int potAmount)
    {
        if (ChatManager.Instance != null)
        {
            if (winners.Count == 1)
            {
                ChatManager.Instance.SendWinnerMessageServerRpc(winners[0], potAmount);
            }
            else
            {
                ChatManager.Instance.SendSplitPotServerRpc(winners.ToArray(), potAmount / winners.Count);
            }
        }
    }

    private void SendNewRoundMessage()
    {
        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.SendGameMessageServerRpc("New round started!");
        }
    }

    private void SendPlayerActionMessage(ulong playerId, IPlayerAction action)
    {
        if (ChatManager.Instance != null)
        {
            BetAction betAction = ConvertToBetAction(action.TypeId);
            ChatManager.Instance.SendPlayerActionServerRpc(playerId, betAction, action.NewBet);
        }
    }

    private void SendTurnChangeMessage(ulong nextPlayerId)
    {
        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.SendTurnChangeServerRpc(nextPlayerId);
        }
    }

    private BetAction ConvertToBetAction(ActionType actionType)
    {
        return actionType switch
        {
            ActionType.FOLD => BetAction.fold,
            ActionType.CHECK => BetAction.check,
            ActionType.CALL => BetAction.call,
            ActionType.RAISE => BetAction.raise,
            ActionType.RERAISE => BetAction.reRaise,
            ActionType.SKIP => BetAction.start,
            _ => BetAction.start
        };
    }
}