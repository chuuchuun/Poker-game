using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

public class BettingControlBehavior : NetworkBehaviour
{
    private List<PlayerController> playerControllers = new List<PlayerController>();
    public List<PlayerController> playersFolded = new List<PlayerController>();

    private NetworkVariable<int> currentHighestBet = new NetworkVariable<int>(0);
    private NetworkVariable<int> currentBank = new NetworkVariable<int>(0);
    private List<ChipModel> bankChips = new List<ChipModel>();

    private NetworkList<PlayerState> playerStates;

    private void Awake()
    {
        playerStates = new NetworkList<PlayerState>();
    }

    public int GetCurrentHighestBet() => currentHighestBet.Value;

    public void InitializeBetting(List<PlayerController> players)
    {
        playerControllers = players;
        playersFolded.Clear();

        playerStates.Clear();

        foreach (var player in players)
        {
            playerStates.Add(new PlayerState
            {
                id = player.OwnerClientId,
                currentBet = 0,
                currentBalance = player.CurrentBalance,
                hasFolded = false
            });
        }

        currentHighestBet.Value = 0;
        currentBank.Value = 0;
        bankChips.Clear();
    }

    public bool ProcessPlayerAction(ulong playerId, IPlayerAction action, PlayerController player)
    {
        var index = FindPlayerStateIndex(playerId);
        if (index == -1) return false;

        var state = playerStates[index];

        if (action.HasFolded)
        {
            state.hasFolded = true;
            playerStates[index] = state;
            playersFolded.Add(player);
            player.ClearHand();
            return true;
        }

        int requiredToCall = currentHighestBet.Value - state.currentBet;

        if (action.TypeId == 3)
        {
            if (requiredToCall == 0)
            {
                return true;
            }
            return false;
        }

        if (action.TypeId == 2)
        {
            if (requiredToCall <= player.CurrentBalance)
            {
                var movedChips = player.RemoveChip(requiredToCall);
                if (movedChips != null)
                {
                    bankChips.AddRange(movedChips);
                    state.currentBalance = player.CurrentBalance;
                    state.currentBet = currentHighestBet.Value;
                    playerStates[index] = state;
                    currentBank.Value += requiredToCall;
                    return true;
                }
            }
            return false;
        }

        if ((action.TypeId == 4 || action.TypeId == 5) && action.NewBet >= requiredToCall)
        {
            int additionalBet = action.NewBet - state.currentBet;
            if (additionalBet <= player.CurrentBalance)
            {
                var movedChips = player.RemoveChip(additionalBet);
                if (movedChips != null)
                {
                    bankChips.AddRange(movedChips);
                    state.currentBalance = player.CurrentBalance;
                    state.currentBet = action.NewBet;
                    playerStates[index] = state;
                    currentBank.Value += additionalBet;

                    if (state.currentBet > currentHighestBet.Value)
                        currentHighestBet.Value = state.currentBet;

                    return true;
                }
            }
        }
        return false;
    }

    public void UpdatePlayerState(PlayerController player)
    {
        var playerId = player.OwnerClientId;
        var index = FindPlayerStateIndex(playerId);

        if (index == -1)
        {
            playerStates.Add(new PlayerState
            {
                id = playerId,
                currentBet = player.currentBet,
                currentBalance = player.CurrentBalance,
                hasFolded = playersFolded.Contains(player)
            });
        }
        else
        {
            var state = playerStates[index];
            state.currentBet = player.currentBet;
            state.currentBalance = player.CurrentBalance;
            state.hasFolded = playersFolded.Contains(player);
            playerStates[index] = state;
        }
    }

    public bool IsPlayerFolded(ulong playerId)
    {
        var index = FindPlayerStateIndex(playerId);
        if (index == -1) return false;

        return playerStates[index].hasFolded;
    }

    public int GetPlayerBet(ulong playerId)
    {
        var index = FindPlayerStateIndex(playerId);
        if (index == -1) return 0;

        return playerStates[index].currentBet;
    }

    public int GetPlayerBalance(ulong playerId)
    {
        var index = FindPlayerStateIndex(playerId);
        if (index == -1) return 0;

        return playerStates[index].currentBalance;
    }

    public void ResetForNewRound()
    {
        currentHighestBet.Value = 0;

        for (int i = 0; i < playerStates.Count; i++)
        {
            var state = playerStates[i];
            state.currentBet = 0;
            state.hasFolded = false;
            playerStates[i] = state;
        }

        playersFolded.Clear();
    }

    public void DistributeWinnings(ulong winnerId, int amount)
    {
        var winner = playerControllers.FirstOrDefault(p => p.OwnerClientId == winnerId);
        if (winner != null)
        {
            winner.CurrentBalance += amount;

            var index = FindPlayerStateIndex(winnerId);
            if (index != -1)
            {
                var state = playerStates[index];
                state.currentBalance = winner.CurrentBalance;
                playerStates[index] = state;
            }
        }
        ClearBank();
        currentBank.Value = 0;

    }

    private void ClearBank()
    {
        foreach (var chip in bankChips)
        {
            if (chip != null && chip.NetworkObject != null && chip.NetworkObject.IsSpawned)
            {
                chip.NetworkObject.Despawn(true);
            }
        }
        bankChips.Clear();
    }


    private int FindPlayerStateIndex(ulong playerId)
    {
        for (int i = 0; i < playerStates.Count; i++)
        {
            if (playerStates[i].id == playerId)
                return i;
        }
        return -1;
    }

    public int GetCurrentBank()
    {
        return currentBank.Value;
    }

    public List<PlayerState> GetAllPlayerStates()
    {
        var list = new List<PlayerState>();
        foreach (var player in playerStates)
        {
            list.Add(player);
        }
        return list;
    }

    public List<ulong> GetActivePlayers()
    {
        List<ulong> activePlayers = new List<ulong>();

        foreach (var state in playerStates)
        {
            if (!state.hasFolded)
            {
                activePlayers.Add(state.id);
            }
        }
        return activePlayers;
    }
}