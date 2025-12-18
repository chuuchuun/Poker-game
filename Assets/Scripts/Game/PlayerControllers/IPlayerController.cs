using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public interface IPlayerController
{
    // Identity / network
    ulong PlayerId { get; }
    ulong OwnerClientId { get; }
    bool IsSpawned { get; }
    bool IsOwner { get; }
    NetworkObject NetworkObject { get; }

    // Balance
    int CurrentBalance { get; set; }
    event Action<int> OnBalanceChanged;
    void AddChipsFromBank(List<ChipModel> chips);
    void AddChipsServerRpc(int amount);
    void UpdateBalanceClientRpc(int newBalance);

    // Betting state
    int CurrentBet { get; set; }

    // Hand / UI
    List<CardModel> CardsInHand { get; }
    List<Transform> CardSlots { get; }
    void ClearHand();

    // Chips
    List<ChipModel> TotalChips { get; }
    List<ChipModel> RemoveChip(int bet);
    void MoveChipsToBankServerRpc(ulong[] chipIds);

    // Spawn helpers
    void SetSpawnIndex(int index);
    int GetSpawnIndex();

    // Actions
    List<BetAction> GetAvailableActions();
    void Act(BetAction action, int newBet = 0);
    void NotifyTurn(bool isMyTurn);
}