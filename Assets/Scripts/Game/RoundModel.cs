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
    private List<PlayerController> playerModels = new List<PlayerController>();
    private List<CardModel> deck = new List<CardModel>();

    public List<CardModel> cardsOnTable = new List<CardModel>();
    public int minimalBet;
    public BettingController bettingController;
    private List<Transform> cardSlots = new List<Transform>();
    private List<Transform> foldSlots = new List<Transform>();
    private List<int> playerSpawns = new List<int>();
    private Vector3 deckPosition;

    private void Awake()
    {
        InitializeDeckAndPlayers();
    }

    private void Start()
    {
        //dealCards(); // Initial card dealing
        //addCardOnTable(5); // Add cards to the table (flop, turn, river)
    }

    private void Update()
    {
        //CheckAndDealCardsToNewPlayers();
    }

    public void StartRound()
    {
        dealCards(); // Initial card dealing
        addCardOnTableServerRpc(2); // Add cards to the table (flop, turn, river)
    }

    private void InitializeDeckAndPlayers()
    {
        CardModel[] allCards = FindObjectsOfType<CardModel>();
        foreach (CardModel card in allCards)
        {
            deck.Add(card);
        }
        deckPosition = allCards[0].transform.position;

        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in allPlayers)
        {
            if (!playerModels.Contains(player))
            {
                playerModels.Add(player);
                Debug.Log("New player added: " + player.name);
                // Optionally deal cards immediately to the new player here
            }
        }

        foreach (Transform child in transform)
        {
            if (child.CompareTag("slot"))
            {
                cardSlots.Add(child);
                //Debug.Log("Slot found: " + child.name);
            }
            else if (child.CompareTag("fold_slot"))
            {
                foldSlots.Add(child);
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
                // Optionally deal cards immediately to the new player here
            }
        }
        // Deal cards to all players at the start of the round
        foreach (PlayerController playerModel in playerModels)
        {
            if (playerModel.IsSpawned)
                //DealCardsToPlayerServerRpc(new NetworkObjectReference(playerModel.GetComponent<NetworkObject>()));
                DealToPlayersServerRpc(new NetworkObjectReference(playerModel.gameObject.GetComponent<NetworkObject>()));
        }
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


                    //GameObject cardObject = playerModel.cardsInHand[i].gameObject;
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
                foreach (Transform slot in cardSlots)
                    if (slot.childCount == 0)
                    {
                        cardModel.transform.Rotate(0, 180f, 0);
                        cardModel.transform.localPosition = Vector3.zero;
                        Debug.Log("Assigned card to slot: " + slot.name);
                        break;
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

                

                foreach (Transform slot in cardSlots)
                {
                    if (slot.childCount == 0)
                    {
                        GameObject cardObject = randomCard.gameObject;
                        cardObject.transform.Rotate(0, 180f, 0);
                        cardObject.transform.SetParent(slot);
                        cardObject.transform.localPosition = Vector3.zero;
                        Debug.Log("Assigned card to slot: " + slot.name);
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