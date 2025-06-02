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
    public List<CardModel> cardsOnTable = new List<CardModel>();
    public int minimalBet;
    public BettingController bettingController;

    private GameObject[] cardSlots;
    private GameObject[] foldSlots;
    private int currentBank = 0;

    private NetworkVariable<int> currentHighestBet = new NetworkVariable<int>(0);
    private List<PlayerState> playerStates = new List<PlayerState>();

    private void Awake()
    {
        InitializePlayersAndSlots();
    }

    public int GetCurrentHighestBet() => currentHighestBet.Value;

    public void StartGame(ulong[] playerIds, ulong firstPlayerId)
    {
        deckControlBehavior.InitializeDeck();
        DealCards();

        queueControlBehavior.SetFirstPlayerToMove(firstPlayerId);
        queueControlBehavior.SetPlayers(playerIds.ToList());

        playerStates = playerIds.Select(id =>
        {
            return new PlayerState()
            {
                id = id,
                currentBet =0,
                hasFolded = false,
            };
        }).ToList();
    }

    private void InitializePlayersAndSlots()
    {
        foldSlots = GameObject.FindGameObjectsWithTag("fold_slot").OrderBy(slot => slot.name).ToArray();
        cardSlots = GameObject.FindGameObjectsWithTag("slot");
        UpdatePlayers();
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

    private void DealCards()
    {
        UpdatePlayers();
        foreach (var player in playerModels.Where(p => p.IsSpawned))
        {
            DealToPlayersServerRpc(
                new NetworkObjectReference(player.GetComponent<NetworkObject>())
            );
        }
    }

    [ServerRpc]
    public void DealToPlayersServerRpc(NetworkObjectReference playerNetwork)
    {
        if (!playerNetwork.TryGet(out NetworkObject playerObject)) return;

        PlayerController playerModel = playerObject.GetComponent<PlayerController>();
        List<CardModel> dealtCards = new List<CardModel>();

        int cardsNeeded = 2 - playerModel.cardsInHand.Count;
        for (int i = 0; i < cardsNeeded; i++)
        {
            CardModel card = deckControlBehavior.DrawRandomCard();
            if (card != null)
            {
                playerModel.cardsInHand.Add(card);
                dealtCards.Add(card);
            }
        }

        for (int i = 0; i < playerModel.cardsInHand.Count; i++)
        {
            var cardObject = playerModel.cardsInHand[i].gameObject;
            var slot = playerModel.cardSlots[i];

            cardObject.transform.SetParent(null);
            cardObject.transform.position = slot.position;
            cardObject.transform.rotation = Quaternion.Euler(0, 180f, 0);
        }

        foreach (var card in dealtCards)
        {
            MoveCardToPlayerClientRpc(playerNetwork, new NetworkObjectReference(card.GetComponent<NetworkObject>()));
        }
    }

    [ClientRpc]
    private void MoveCardToPlayerClientRpc(NetworkObjectReference playerNetwork, NetworkObjectReference cardNetwork)
    {
        if (NetworkManager.IsHost) return;

        if (playerNetwork.TryGet(out var playerObj) && cardNetwork.TryGet(out var cardObj))
        {
            var player = playerObj.GetComponent<PlayerController>();
            var card = cardObj.GetComponent<CardModel>();

            if (player.cardsInHand.Count < 2)
            {
                player.cardsInHand.Add(card);
                var slot = player.cardSlots[player.cardsInHand.Count - 1];
                card.transform.SetParent(null);
                card.transform.position = slot.position;
                card.transform.rotation = Quaternion.Euler(0, 180f, 0);
            }
        }
    }

    public void TryToMakePlayerAction(IPlayerAction playerAction)
    {
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
        TryToMakePlayerAction(rpcParams.Receive.SenderClientId, networkPlayerAction.Value);
    }

    private void TryToMakePlayerAction(ulong playerId, IPlayerAction action)
    {
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
                    player.RemoveChip(additionalBet);
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

    [ServerRpc]
    public void AddCardToTableServerRpc(int count)
    {
        for (int i = 0; i < count; i++)
        {
            CardModel card = deckControlBehavior.DrawRandomCard();
            if (card == null) break;

            cardsOnTable.Add(card);

            foreach (var slot in cardSlots)
            {
                if (slot.transform.childCount == 0)
                {
                    var cardObj = card.gameObject;
                    cardObj.transform.SetParent(slot.transform);
                    cardObj.transform.localPosition = Vector3.zero;
                    cardObj.transform.rotation = Quaternion.Euler(0, 180f, 0);
                    AddCardToTableClientRpc(new NetworkObjectReference(card.GetComponent<NetworkObject>()));
                    break;
                }
            }
        }
    }

    [ClientRpc]
    private void AddCardToTableClientRpc(NetworkObjectReference cardNetwork)
    {
        if (NetworkManager.IsHost) return;

        if (cardNetwork.TryGet(out var cardObject))
        {
            var card = cardObject.gameObject;
            foreach (var slot in cardSlots)
            {
                if (slot.transform.childCount == 0)
                {
                    card.transform.SetParent(slot.transform);
                    card.transform.localPosition = Vector3.zero;
                    card.transform.rotation = Quaternion.Euler(0, 180f, 0);
                    break;
                }
            }
        }
    }
}
