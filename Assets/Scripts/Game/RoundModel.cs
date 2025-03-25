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

    public void dealCards()
    {
        for (int j = 0; j < 2; j++)
        {
            foreach (PlayerController playerModel in playerModels)
            {
                CardModel randomCard = deck[Random.Range(0, deck.Count)];
                playerModel.cardsInHand.Add(randomCard);
                deck.Remove(randomCard);
            }
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


    private void Awake()
    {
        CardModel[] allCards = FindObjectsOfType<CardModel>();
        foreach (CardModel card in allCards)
        {
            deck.Add(card);
        }

        PlayerController[] allplayers = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in allplayers)
        {
            playerModels.Add(player);
        }

        foreach (Transform child in transform)
        {
            if (child.CompareTag("slot"))
            {
                cardSlots.Add(child);
                Debug.Log(child);
            }
            
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        dealCards();
        addCardOnTable(5);
    }

    // Update is called once per frame
    void Update()
    {
    }
}
