using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public interface IPlayerController
{
    ulong PlayerId { get; }
    ulong OwnerClientId { get; }
    bool IsSpawned { get; }
    bool IsOwner { get; }
    NetworkObject NetworkObject { get; }

    int CurrentBalance { get; set; }
    event Action<int> OnBalanceChanged;
    void AddChipsFromBank(List<ChipModel> chips);
    void AddChipsServerRpc(int amount);
    void UpdateBalanceClientRpc(int newBalance);

    int CurrentBet { get; set; }

    List<CardModel> CardsInHand { get; }
    List<Transform> CardSlots { get; }
    void ClearHand();

    List<ChipModel> TotalChips { get; }
    List<ChipModel> RemoveChip(int bet);
    void MoveChipsToBankServerRpc(ulong[] chipIds);

    void SetSpawnIndex(int index);
    int GetSpawnIndex();

    List<BetAction> GetAvailableActions();
    void Act(BetAction action, int newBet = 0);
    void NotifyTurn(bool isMyTurn);

    void Kick();
}