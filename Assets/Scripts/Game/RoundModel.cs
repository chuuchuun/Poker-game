using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RoundModel : NetworkBehaviour
{
    private List<PlayerController> playerModels = new List<PlayerController>();
    private List<CardModel> deck = new List<CardModel>();

    public List<CardModel> cardsOnTable = new List<CardModel>();
    public int minimalBet;
    public BettingController bettingController;
    private List<Transform> cardSlots = new List<Transform>();

    private void Awake()
    {
        InitializeDeckAndPlayers();
    }

    private void Start()
    {
        dealCards(); // Initial card dealing
        addCardOnTable(5); // Add cards to the table (flop, turn, river)
    }

    private void Update()
    {
        CheckAndDealCardsToNewPlayers();
    }

    private void InitializeDeckAndPlayers()
    {
        CardModel[] allCards = FindObjectsOfType<CardModel>();
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
        }
    }

    private void CheckAndDealCardsToNewPlayers()
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
        //InitializeDeckAndPlayers();
        // Loop through all player controllers and check if they already have cards
        
        
            foreach (PlayerController playerModel in playerModels)
            {
                // Deal cards only if the player doesn't already have cards
                if (playerModel.cardsInHand.Count == 0 && playerModel.IsSpawned)
                {
                    Debug.Log("Dealing cards to new player: " + playerModel.name);
                    //DealCardsToPlayer(playerModel);
                    DealCardsToPlayerServerRpc(new NetworkObjectReference(playerModel.GetComponent<NetworkObject>()));
                }
            }
       
    }

    [ServerRpc(RequireOwnership = false)]
    private void DealCardsToPlayerServerRpc(NetworkObjectReference playerNetwork)
    {
        if(playerNetwork.TryGet(out NetworkObject playerObject))
        {
            PlayerController playerModel = playerObject.gameObject.GetComponent<PlayerController>();
            if(playerModel.cardsInHand.Count < 2)
            {
                int limit = 2 - playerModel.cardsInHand.Count;
                for (int i = 0; i < limit; i++)
                {
                    CardModel randomCard = deck[Random.Range(0, deck.Count)];
                    playerModel.cardsInHand.Add(randomCard);
                    deck.Remove(randomCard);
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
            }

            VisualizeServerDealtCardsClientRpc(playerNetwork);
        }
    }

    [ClientRpc]
    private void VisualizeServerDealtCardsClientRpc(NetworkObjectReference playerNetwork)
    {
        if(playerNetwork.TryGet(out NetworkObject playerObject))
        {
            PlayerController playerModel = playerObject.gameObject.GetComponent<PlayerController>();
            for (int i = 0; i < playerModel.cardsInHand.Count; i++)
            {
                GameObject cardObject = playerModel.cardsInHand[i].gameObject;
                Transform slot = playerModel.cardSlots[i];

                cardObject.transform.SetParent(null);
                cardObject.transform.position = slot.position;
                cardObject.transform.rotation = Quaternion.Euler(0, 180f, 0);

                Debug.Log($"(Client) Assigned card to slot: {slot.name}");
            }
        }
    }

    /*[ClientRpc]
    private void DealSingleCardToPlayerClientRpc(NetworkObjectReference playerNetwork, NetworkObjectReference cardNetwork)
    {
        if(playerNetwork.TryGet(out NetworkObject playerObject) && cardNetwork.TryGet(out NetworkObject cardObject))
        {
            PlayerController playerModel = playerObject.gameObject.GetComponent<PlayerController>();
            CardModel cardModel = cardObject.gameObject.GetComponent<CardModel>();

            playerModel.cardsInHand.Add(cardModel);

            int position = 0;
            if (cardModel == playerModel.cardsInHand[1])
                position = 1;

            cardObject.transform.SetParent(null);
            cardObject.transform.position = playerModel.cardSlots[position].position;
            cardObject.transform.rotation = Quaternion.Euler(0, 180f, 0);

            Debug.Log("Something!");
        }
    }

    private void DealCardsToPlayer(PlayerController playerModel)
    {
        HashSet<int> usedSlots = new HashSet<int>(); // Track assigned slots

        for (int i = 0; i < 2; i++)
        {
            CardModel randomCard = deck[Random.Range(0, deck.Count)];
            playerModel.cardsInHand.Add(randomCard);
            deck.Remove(randomCard);
        }

        foreach (CardModel card in playerModel.cardsInHand)
        {
            GameObject cardObject = card.gameObject;

            for (int i = 0; i < playerModel.cardSlots.Count; i++)
            {
                if (!usedSlots.Contains(i)) // Check if slot is free
                {
                    Transform slot = playerModel.cardSlots[i];

                    cardObject.transform.SetParent(null);
                    cardObject.transform.position = slot.position;
                    cardObject.transform.rotation = Quaternion.Euler(0, 180f, 0);

                    usedSlots.Add(i); // Mark slot as used
                    Debug.Log($"Assigned card to slot: {slot.name}");
                    break; // Stop searching for a slot once assigned
                }
            }
        }
    }*/



    public void dealCards()
    {
        // Deal cards to all players at the start of the round
        foreach (PlayerController playerModel in playerModels)
        {
            if(playerModel.IsSpawned)
                DealCardsToPlayerServerRpc(new NetworkObjectReference(playerModel.GetComponent<NetworkObject>()));
        }
    }

    public void addCardOnTable(int cardCount)
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
            }
        }
    }
}