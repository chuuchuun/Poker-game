using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DeckControlBehavior : NetworkBehaviour
{
    private List<CardModel> deck = new List<CardModel>();

    private void Awake()
    {
        InitializeDeck();
    }

    public void InitializeDeck()
    {
        deck.Clear();
        CardModel[] allCards = FindObjectsOfType<CardModel>();
        foreach (CardModel card in allCards)
        {
            deck.Add(card);
        }
    }

    public CardModel DrawRandomCard()
    {
        if (deck.Count == 0) return null;
        var card = deck[Random.Range(0, deck.Count)];
        deck.Remove(card);
        return card;
    }

    public void ReturnCard(CardModel card)
    {
        if (!deck.Contains(card))
            deck.Add(card);
    }

    public int GetRemainingCardCount() => deck.Count;
}
