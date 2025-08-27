using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class RoundModel : NetworkBehaviour
{
    private QueueControlBehavior queueControlBehavior => GameManager.Instance.GetComponent<QueueControlBehavior>();
    private DeckControlBehavior deckControlBehavior => GameManager.Instance.GetComponent<DeckControlBehavior>();

    private List<PlayerController> playerModels = new List<PlayerController>();
    public int minimalBet;
    public BettingController bettingController;

    private int currentBank = 0;

    private NetworkVariable<int> currentHighestBet = new NetworkVariable<int>(0);
    private List<PlayerState> playerStates = new List<PlayerState>();
    private List<ChipModel> bankChips = new List<ChipModel>();

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[RoundModel] OnNetworkSpawn | ObjId={NetworkObjectId} | IsServer={IsServer} | IsOwner={IsOwner}");
    }

    private void Awake()
    {
        UpdatePlayers();
    }

    private void Start()
    {
        Debug.Log($"[RoundModel] Pre-RPC check | IsSpawned={NetworkObject.IsSpawned} | ObjId={NetworkObjectId} | IsServer={IsServer} | IsOwner={IsOwner}");
    }

    public int GetCurrentHighestBet() => currentHighestBet.Value;

    public void StartGame(ulong[] playerIds, ulong firstPlayerId)
    {
        deckControlBehavior.InitializeDeckAndSlots();
        UpdatePlayers();
        deckControlBehavior.DealCards(playerModels);

        queueControlBehavior.SetFirstPlayerToMove(firstPlayerId);
        queueControlBehavior.SetPlayers(playerIds.ToList());

        playerStates = playerIds.Select(id =>
            new PlayerState { id = id, currentBet = 0, hasFolded = false }
        ).ToList();
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

    // === Вызов действия игрока ===
    public void TryToMakePlayerAction(IPlayerAction action)
    {
        Debug.Log($"[RoundModel] TryToMakePlayerAction | IsSpawned={IsSpawned} | IsServer={IsServer}");

        if (IsServer)
        {
            // Сервер может вызвать напрямую
            ProcessPlayerAction(NetworkManager.Singleton.LocalClientId, action);
        }
        else
        {
            // Клиент → отправляем RPC на сервер
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
        ulong senderId = rpcParams.Receive.SenderClientId;
        IPlayerAction action = networkAction.ToAction();

        Debug.Log($"[SERVER] Received action type {networkAction.ActionType} with bet {networkAction.BetAmount} from {senderId}");
        Debug.Log($"[SERVER] Converted to: {action.GetType().Name}");

        ProcessPlayerAction(senderId, action);

        // После обработки на сервере уведомляем всех клиентов
        PlayerActionProcessedClientRpc(senderId, networkAction.ActionType, networkAction.BetAmount, networkAction);
    }

    [ClientRpc]
    private void PlayerActionProcessedClientRpc(ulong playerId, int actionType, int betAmount, NetworkPlayerAction networkAction)
    {
        Debug.Log($"[CLIENT] Player {playerId} made action={actionType}, bet={betAmount}");
        IPlayerAction action = networkAction.ToAction();
        ProcessPlayerAction(playerId, action);
    }

    private void ProcessPlayerAction(ulong playerId, IPlayerAction action)
    {
        Debug.Log($"[RoundModel] Processing {action.GetType().Name} for player {playerId}");

        if (!queueControlBehavior.ShouldAcceptActionFromPlayerWithId(playerId)) return;

        var index = playerStates.FindIndex(s => s.id == playerId);
        if (index == -1) return;

        var state = playerStates[index];
        var player = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(playerId)?.GetComponent<PlayerController>();
        if (player == null) return;

        if (action.HasFolded)
        {
            state.hasFolded = true;
            player.ClearHand();
        }
        else
        {
            int requiredToCall = currentHighestBet.Value - state.currentBet;
            int bet = action.NewBet;

            // Check only if bet is valid
            if (action.TypeId == 3 && currentHighestBet.Value != 0) return;

            if ((action.TypeId == 2 || action.TypeId == 4 || action.TypeId == 5) && bet >= requiredToCall)
            {
                int additionalBet = bet - state.currentBet;
                if (additionalBet <= player.currentBalance)
                {
                    var movedChips = player.RemoveChip(additionalBet);
                    if (movedChips != null)
                        bankChips.AddRange(movedChips);

                    state.currentBalance = player.currentBalance;
                    state.currentBet = bet;
                    currentBank += additionalBet;

                    if (state.currentBet > currentHighestBet.Value)
                        currentHighestBet.Value = state.currentBet;
                }
            }
        }

        playerStates[index] = state;
        queueControlBehavior.SwitchTurnToNextPlayer();
        UpdatePlayersState();
    }

    private void UpdatePlayersState()
    {
        if (!IsServer) return;

        foreach (var player in playerModels)
        {
            var playerId = player.OwnerClientId;
            var index = playerStates.FindIndex(p => p.id == playerId);

            var state = new PlayerState
            {
                id = playerId,
                currentBet = player.currentBet,
                currentBalance = player.currentBalance,
                hasFolded = false
            };

            if (index == -1)
                playerStates.Add(state);
            else
                playerStates[index] = state;
        }

        // Рассылаем состояние клиентам
    }

}
