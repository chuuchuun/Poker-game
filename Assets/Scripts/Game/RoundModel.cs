using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class RoundModel : NetworkBehaviour
{
    private QueueControlBehavior queueControlBehavior
    {
        get
        {
            return GameManager.Instance.GetComponent<QueueControlBehavior>();
        }
    }

    private List<PlayerController> playerModels = new List<PlayerController>();
    private List<CardModel> deck = new List<CardModel>();

    public List<CardModel> cardsOnTable = new List<CardModel>();
    public int minimalBet;
    public BettingController bettingController;
    private GameObject[] cardSlots;
    private GameObject[] foldSlots;
    private List<int> playerSpawns = new List<int>();
    private Vector3 deckPosition;
    private int currentBank = 0;

    private NetworkVariable<int> currentHighestBet = new NetworkVariable<int> (0);

    private NetworkList<PlayerState> playerStates = new NetworkList<PlayerState>();
    private void Awake()
    {
        InitializeDeckAndPlayers();
    }

    public int GetCurrentHighestBet()
    {
        return currentHighestBet.Value;
    }
    public void StartGame(ulong[] playerIds, ulong firstPlayerId)
    {
        dealCards();
        queueControlBehavior.SetFirstPlayerToMove(firstPlayerId);
        queueControlBehavior.SetPlayers(playerIds.ToList());

        var mappedStates = playerIds.Select(id => new PlayerState
        {
            id = id,
            currentBet = 0,
            hasFolded = false
        });

        foreach (var state in mappedStates)
        {
            playerStates.Add(state);
        }
    }


    private void InitializeDeckAndPlayers()
    {
        CardModel[] allCards = FindObjectsOfType<CardModel>();

        // Posortuj foldSloty według nazw (foldSlot1, foldSlot2, itd.)
        foldSlots = GameObject.FindGameObjectsWithTag("fold_slot")
                             .OrderBy(slot => slot.name)
                             .ToArray();

        cardSlots = GameObject.FindGameObjectsWithTag("slot");

        foreach (CardModel card in allCards)
        {
            deck.Add(card);
        }

        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in allPlayers)
        {
            if (!playerModels.Contains(player))
            {
                playerModels.Add(player);
                Debug.Log("New player added: " + player.name);
            }
        }
    }

    public void dealCards()
    {
        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in allPlayers)
        {
            if (!playerModels.Contains(player))
            {
                playerModels.Add(player);
                Debug.Log("New player added: " + player.name);
            }
        }

        foreach (PlayerController playerModel in playerModels)
        {
            if (playerModel.IsSpawned)
                DealToPlayersServerRpc(new NetworkObjectReference(playerModel.gameObject.GetComponent<NetworkObject>()));
        }
    }

    public void TryToMakePlayerAction(IPlayerAction playerAction)
    {
        if (IsServer)
        {
            TryToMakePlayerAction(0, playerAction);
        } else
        {
            NetworkPlayerAction networkAction = new NetworkPlayerAction
            {
                Value = playerAction
            };

            TryToMakePlayerActionServerRpc(networkAction);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TryToMakePlayerActionServerRpc(
        NetworkPlayerAction networkPlayerAction,
        ServerRpcParams rpcParams = default
    )
    {
        IPlayerAction playerAction = networkPlayerAction.Value;
        ulong playerId = rpcParams.Receive.SenderClientId;
        TryToMakePlayerAction(playerId, playerAction);
    }

    private void UpdatePlayersState()
    {
        if (!IsServer) return;

        for (int i = 0; i < playerModels.Count; i++)
        {
            var player = playerModels[i];
            ulong playerId = player.OwnerClientId;

            // Find or create state
            int stateIndex = -1;
            for (int j = 0; j < playerStates.Count; j++)
            {
                if (playerStates[j].id == playerId)
                {
                    stateIndex = j;
                    break;
                }
            }

            PlayerState state;
            if (stateIndex == -1)
            {
                state = new PlayerState
                {
                    id = playerId,
                    currentBet = player.currentBet,
                    currentBalance = player.currentBalance,
                    hasFolded = false,
                };
                playerStates.Add(state);
            }
            else
            {
                state = playerStates[stateIndex];
                state.currentBet = player.currentBet;
                state.currentBalance = player.currentBalance;
                playerStates[stateIndex] = state;
            }

            player.currentBalance = state.currentBalance;
            player.currentBet = state.currentBet;
        }
    }

    private void TryToMakePlayerAction(ulong playerId, IPlayerAction playerAction)
    {
        if (!queueControlBehavior.ShouldAcceptActionFromPlayerWithId(playerId))
        {
            Debug.Log($"QUEUE: Declined action from {playerId}");
            return;
        }

        int index = -1;
        for (int i = 0; i < playerStates.Count; i++)
            if (playerStates[i].id == playerId) index = i;

        var state = playerStates[index];
        Debug.Log($"QUEUE: Accepted action {playerAction.TypeId} from {playerId}");

        NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(playerId);
        if (playerNetworkObject == null)
        {
            Debug.LogWarning($"No network object found for player {playerId}");
            return;
        }

        PlayerController player = playerNetworkObject.GetComponent<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning($"No PlayerController found on player object {playerId}");
            return;
        }

        if (playerAction.HasFolded)
        {
            state.hasFolded = true;
            playerStates[index] = state;
            player.ClearHand();
            Debug.Log($"Player {playerId} folded.");
        }
        else
        {
            if (playerAction.TypeId == 3)
            {
                if(currentHighestBet.Value != 0)
                {
                    Debug.LogWarning($"PLayer tried to check but the current highest bet is: {currentHighestBet}");
                    return;
                }
                Debug.Log($"Player {playerId} checked.");

            }
            else if (playerAction.TypeId == 2 || playerAction.TypeId == 4 || playerAction.TypeId == 5) // call / raise / reraise
            {
                int requiredToCall = currentHighestBet.Value - state.currentBet;
                int bet = playerAction.NewBet;

                if (bet < requiredToCall)
                {
                    Debug.LogWarning($"Player {playerId} bet too low: {bet}, required: {requiredToCall}");
                    return;
                }

                int additionalBet = bet - state.currentBet;
                if (additionalBet > player.currentBalance)
                {
                    Debug.LogWarning($"Player {playerId} doesn't have enough chips. Needed: {additionalBet}, has: {state.currentBalance}");
                    return;
                }

                // Remove chips first – this updates player.currentBalance
                player.RemoveChip(additionalBet);

                // Then update the state from the player object
                state.currentBalance = player.currentBalance;
                state.currentBet = bet;
                playerStates[index] = state;

                player.currentBet = state.currentBet;

                currentBank += additionalBet;

                if (state.currentBet > currentHighestBet.Value)
                {
                    currentHighestBet.Value = state.currentBet;
                    Debug.Log($"Player {playerId} raised to {state.currentBet}. New balance: {state.currentBalance}");
                }
                else if (additionalBet == 0)
                {
                    Debug.Log($"Player {playerId} called with {additionalBet}. New balance: {state.currentBalance}");
                }
            }
        }

        queueControlBehavior.SwitchTurnToNextPlayer();
        UpdatePlayersState();
    }

    [ServerRpc]
    public void DealToPlayersServerRpc(NetworkObjectReference playerNetwork)
    {
        if (playerNetwork.TryGet(out NetworkObject playerObject))
        {
            List<CardModel> cards = new List<CardModel>();
            PlayerController playerModel = playerObject.gameObject.GetComponent<PlayerController>();
            int limit = 2 - playerModel.cardsInHand.Count;
            for (int i = 0; i < limit; i++)
            {
                CardModel randomCard = deck[Random.Range(0, deck.Count)];
                playerModel.cardsInHand.Add(randomCard);
                deck.Remove(randomCard);
                cards.Add(randomCard);
            }

            for (int i = 0; i < playerModel.cardsInHand.Count; i++)
            {
                GameObject cardObject = playerModel.cardsInHand[i].gameObject;
                Transform slot = playerModel.cardSlots[i];

                cardObject.transform.SetParent(null);
                cardObject.transform.position = slot.position;
                cardObject.transform.rotation = Quaternion.Euler(0, 180f, 0);

                Debug.Log($"(Server) Assigned card to slot: {slot.name}");

                
            }

            foreach(CardModel card in cards)
            {
                MoveCardToPlayerClientRpc(playerNetwork, new NetworkObjectReference(card.gameObject.GetComponent<NetworkObject>()));
            }
        }
    }
    [ClientRpc]
    private void MoveCardToPlayerClientRpc(NetworkObjectReference playerNetwork, NetworkObjectReference cardNetwork)
    {
        if (!NetworkManager.IsHost)
        {
            if (playerNetwork.TryGet(out NetworkObject playerObject) && cardNetwork.TryGet(out NetworkObject cardObject))
            {
                PlayerController playerModel = playerObject.gameObject.GetComponent<PlayerController>();
                Debug.Log(playerModel.cardsInHand.Count);
                if (playerModel.cardsInHand.Count < 2)
                {
                    GameObject cardModel = cardObject.gameObject;

                    deck.Remove(cardModel.GetComponent<CardModel>());

                    playerModel.cardsInHand.Add(cardModel.GetComponent<CardModel>());
                    Transform slot = playerModel.cardSlots[playerModel.cardsInHand.Count - 1];

                    cardModel.transform.SetParent(null);
                    cardModel.transform.position = slot.position;
                    cardModel.transform.rotation = Quaternion.Euler(0, 180f, 0);

                    Debug.Log($"(Client) Assigned card to slot: {slot.name}");
                }
            }
        }
    }

    [ClientRpc]
    private void addCardOnTableClientRpc(NetworkObjectReference cardNetwork)
    {
        if (!NetworkManager.IsHost)
        {
            if (cardNetwork.TryGet(out NetworkObject cardObject))
            {
                GameObject cardModel = cardObject.gameObject;
                deck.Remove(cardModel.GetComponent<CardModel>());
                Transform transform = null;
                foreach (GameObject slot in cardSlots)
                {
                    transform = slot.transform;
                    if (transform.childCount == 0)
                    {
                        cardModel.transform.Rotate(0, 180f, 0);
                        cardModel.transform.localPosition = Vector3.zero;
                        Debug.Log("Assigned card to slot: " + transform.name);
                        break;
                    }
                }
            }
        }
    }

    [ServerRpc]
    public void addCardOnTableServerRpc(int cardCount)
    {
        if (NetworkManager.IsHost)
        {

            for (int i = 0; i < cardCount; i++)
            {
                if (deck.Count == 0)
                {
                    Debug.LogWarning("No more cards in the deck!");
                    break;
                }


                CardModel randomCard = deck[Random.Range(0, deck.Count)];
                cardsOnTable.Add(randomCard);
                deck.Remove(randomCard);


                Transform transform = null;
                foreach (GameObject slot in cardSlots)
                {
                    transform = slot.transform;
                    if (transform.childCount == 0)
                    {
                        GameObject cardObject = randomCard.gameObject;
                        cardObject.transform.Rotate(0, 180f, 0);
                        cardObject.transform.SetParent(transform);
                        cardObject.transform.localPosition = Vector3.zero;
                        Debug.Log("Assigned card to slot: " + transform.name);
                        break;
                    }
                }

                addCardOnTableClientRpc(new NetworkObjectReference(randomCard.gameObject.GetComponent<NetworkObject>()));
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void BackToDeckServerRpc(NetworkObjectReference playerNetwork)
    {
        if (playerNetwork.TryGet(out NetworkObject playerObject))
        {
            PlayerController playerModel = playerObject.GetComponent<PlayerController>();

            bool isFirstCard = true;
            CardModel[] allCards = FindObjectsOfType<CardModel>();
            NetworkObject card1 = null, card2 = null;
            if (playerModel.cardsInHand.Count > 0) {
                Vector3[] cardPositions = { playerModel.cardSlots[0].transform.position, playerModel.cardSlots[1].transform.position };
                foreach (CardModel card in allCards)
                {
                    if (cardPositions.Contains(card.transform.position))
                    {
                        card.transform.position = foldSlots[playerModel.GetSpawnIndex() * 2 + (isFirstCard ? 0 : 1)].transform.position;
                        card.transform.rotation = Quaternion.Euler(-90f, 0, 0);
                        //BackToDeckClientRpc(new NetworkObjectReference(card.gameObject.GetComponent<NetworkObject>()), playerNetwork);
                        if (card1 == null)
                        {
                            card1 = card.GetComponent<NetworkObject>();
                        }
                        else
                        {
                            card2 = card.GetComponent<NetworkObject>();
                        }
                        isFirstCard = false;
                    }
                }
                playerModel.cardSlots.Clear();
                BackToDeckClientRpc(new NetworkObjectReference(card1), new NetworkObjectReference(card2), playerModel.GetSpawnIndex());
            }
            else
            {
                Debug.Log("This player didn't have any cards");
            }
        }
    }

    [ClientRpc]
    private void BackToDeckClientRpc(NetworkObjectReference cardNetwork, NetworkObjectReference card2Network,
        int playerIndex)
    {
        if (!NetworkManager.IsHost)
        {
            if(cardNetwork.TryGet(out NetworkObject cardObject) && card2Network.TryGet(out NetworkObject card2Object))
            {
                CardModel card = cardObject.GetComponent<CardModel>();
                CardModel card2 = card2Object.GetComponent<CardModel>();

                card.transform.rotation = Quaternion.Euler(-90f, 0, 0);
                card.transform.position = foldSlots[playerIndex * 2].transform.position;
                Debug.Log("Card1 position is " + foldSlots[playerIndex * 2].transform.position);

                card2.transform.rotation = Quaternion.Euler(-90f, 0, 0);
                card2.transform.position = foldSlots[playerIndex * 2 + 1].transform.position;
                Debug.Log("Card2 position is " + foldSlots[playerIndex * 2 + 1].transform.position);

            }
        }
    }
}