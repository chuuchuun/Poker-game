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

    private List<PlayerController> playerModels = new List<PlayerController>();
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

    public void StartGame(ulong[] playerIds, ulong firstPlayerId)
    {
        roundStage = RoundStage.GAME;
        UpdatePlayers();
        deckControlBehavior.DealCards(playerModels);

        queueControlBehavior.SetFirstPlayerToMove(firstPlayerId);
        queueControlBehavior.SetPlayers(playerIds.ToList());

        bettingController.InitializeBetting(playerModels);

        if (IsServer)
        {
            SendRoundStartMessage(firstPlayerId);
        }
    }

    private void UpdatePlayers()
    {
        foreach (PlayerController player in FindObjectsOfType<PlayerController>())
        {
            if (!playerModels.Contains(player))
            {
                playerModels.Add(player);
                Debug.Log("New player added: " + player.name);
            }
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

        StartCoroutine(Delay(5, () =>
        {
            List<CardModel> tableCards = deckControlBehavior.GetCardsOnTable();
            List<(ulong, List<CardModel>)> playerHands = playerModels
                .Select(model => (model.playerId, model.cardsInHand))
                .ToList();

            List<ulong> winners = HandEvaluator.GetWinners(playerHands, tableCards);
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

            Debug.Log("Round cleanup started");
            deckControlBehavior.CollectAllCards();
            roundStage = RoundStage.PREPARATION;

            StartCoroutine(Delay(5, () =>
            {
                roundStage = RoundStage.GAME;
                UpdatePlayers();
                deckControlBehavior.DealCards(playerModels);

                bettingController.InitializeBetting(playerModels);

                if (IsServer)
                {
                    SendNewRoundMessage();
                }
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
            ulong playerId = NetworkManager.Singleton.LocalClientId;
            SendPlayerActionMessage(playerId, action);
            ProcessPlayerAction(playerId, action);
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
            ProcessPlayerAction(playerId, action);
        }
    }

    private void ProcessPlayerAction(ulong playerId, IPlayerAction action)
    {
        if (roundStage != RoundStage.GAME) return;

        Debug.Log($"[RoundModel] Processing {action.GetType().Name} for player {playerId}");

        if (!queueControlBehavior.ShouldAcceptActionFromPlayerWithId(playerId)) return;

        var player = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(playerId)?.GetComponent<PlayerController>();
        if (player == null) return;

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