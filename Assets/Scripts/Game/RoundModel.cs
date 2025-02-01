using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundModel : MonoBehaviour
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
        // Check for any new players and deal them cards if necessary
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
            if (playerModel.cardsInHand.Count == 0)
            {
                Debug.Log("Dealing cards to new player: " + playerModel.name);
                DealCardsToPlayer(playerModel);
            }
        }
    }

    private void DealCardsToPlayer(PlayerController playerModel)
    {
        // Deal 2 cards to the player
        for (int i = 0; i < 2; i++)
        {
            CardModel randomCard = deck[Random.Range(0, deck.Count)];
            playerModel.cardsInHand.Add(randomCard);
            deck.Remove(randomCard);
        }
    }

    public void dealCards()
    {
        // Deal cards to all players at the start of the round
        foreach (PlayerController playerModel in playerModels)
        {
            DealCardsToPlayer(playerModel);
        }
    }

    public void addCardOnTable(int cardCount)
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