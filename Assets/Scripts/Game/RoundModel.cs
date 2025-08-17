using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
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

    // Add this method to clean up between rounds
    public void ClearBank()
    {
        if (!IsServer) return;

        foreach (var chip in bankChips)
        {
            if (chip != null)
            {
                chip.GetComponent<NetworkObject>().Despawn();
            }
        }
        bankChips.Clear();
        currentBank = 0;
    }
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Debug.Log("RoundModel spawned on server");
        }
        else
        {
            Debug.Log($"RoundModel spawned on client {NetworkManager.LocalClientId}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnRoundModelServerRpc()
    {
        // Already spawned, do nothing
    }


    private void Awake()
    {
        UpdatePlayers();
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
        {
            return new PlayerState()
            {
                id = id,
                currentBet = 0,
                hasFolded = false,
            };
        }).ToList();
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

    public void TryToMakePlayerAction(IPlayerAction playerAction)
    {
        Debug.Log("Round model trying to make player action");
        if (IsServer)
        {
            TryToMakePlayerAction(0, playerAction);
        }
        else
        {
            TryToMakePlayerActionServerRpc(new NetworkPlayerAction { Value = playerAction });
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TryToMakePlayerActionServerRpc(NetworkPlayerAction networkPlayerAction, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"Received action from client {rpcParams.Receive.SenderClientId}");
        TryToMakePlayerAction(rpcParams.Receive.SenderClientId, networkPlayerAction.Value);
    }
    private void TryToMakePlayerAction(ulong playerId, IPlayerAction action)
    {
        Debug.Log($"Trying to make action for player {playerId}");

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

            if (action.TypeId == 3 && currentHighestBet.Value != 0) return;

            if ((action.TypeId == 2 || action.TypeId == 4 || action.TypeId == 5) && bet >= requiredToCall)
            {
                int additionalBet = bet - state.currentBet;
                if (additionalBet <= player.currentBalance)
                {
                    var movedChips = player.RemoveChip(additionalBet);
                    if (movedChips != null)
                    {
                        bankChips.AddRange(movedChips);
                    }
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
    }
}