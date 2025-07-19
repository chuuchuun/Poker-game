using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class DeckControlBehavior : NetworkBehaviour
{
    public List<CardModel> cardsOnTable = new List<CardModel>();

    private List<CardModel> deck = new List<CardModel>();
    private GameObject[] cardSlots;
    private GameObject[] foldSlots;

    private void Awake()
    {
        InitializeDeckAndSlots();
    }

    public void InitializeDeckAndSlots()
    {
        deck.Clear();
        CardModel[] allCards = FindObjectsOfType<CardModel>();
        foreach (CardModel card in allCards)
        {
            deck.Add(card);
        }

        cardSlots = GameObject
            .FindGameObjectsWithTag("slot")
            .OrderBy(slot => {
                string name = slot.name;
                string numberStr = new string(name.Where(char.IsDigit).ToArray());
                return int.TryParse(numberStr, out int number) ? number : 0;
            })
            .ToArray();
        foldSlots = GameObject
                        .FindGameObjectsWithTag("fold_slot")
                        .OrderBy(slot =>
                        {
                            string name = slot.name;
                            string numberStr = new string(name.Where(char.IsDigit).ToArray());
                            return int.TryParse(numberStr, out int number) ? number : 0;
                        })
                        .ToArray();
    }

    public void DealCards(List<PlayerController> playerModels)
    {
        
        foreach (var player in playerModels.Where(p => p.IsSpawned))
        {
            DealToPlayersServerRpc(
                new NetworkObjectReference(player.GetComponent<NetworkObject>())
            );
        }
    }

    [ServerRpc]
    private void DealToPlayersServerRpc(NetworkObjectReference playerNetwork)
    {
        if (!playerNetwork.TryGet(out NetworkObject playerObject)) return;

        PlayerController playerModel = playerObject.GetComponent<PlayerController>();
        List<CardModel> dealtCards = new List<CardModel>();

        int cardsNeeded = 2 - playerModel.cardsInHand.Count;
        for (int i = 0; i < cardsNeeded; i++)
        {
            CardModel card = DrawRandomCard();
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

    [ServerRpc]
    public void AddCardOnTableServerRpc(int cardCount)
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

                AddCardToTableClientRpc(new NetworkObjectReference(randomCard.gameObject.GetComponent<NetworkObject>()));
            }
        }
    }

    public void ReturnCard(CardModel card)
    {
        PlayerController player = FindObjectsOfType<PlayerController>()
            .FirstOrDefault(p => p.cardsInHand.Contains(card));

        if (player != null)
        {
            int playerIndex = player.GetSpawnIndex();
            int foldSlotStart = playerIndex * 2;
            int foldSlotEnd = foldSlotStart + 2;
            

            for (int i = foldSlotStart; i < foldSlotEnd && i < foldSlots.Length; i++)
            {
                if (foldSlots[i].transform.childCount == 0)
                {
                    card.transform.SetParent(foldSlots[i].transform);
                    card.transform.localPosition = Vector3.zero;
                    card.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    break;
                }
            }
        }
        else
        {
            card.transform.SetParent(null);
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
                    card.transform.rotation = Quaternion.Euler(0, 360f, 0);
                    break;
                }
            }
        }
    }

    private CardModel DrawRandomCard()
    {
        if (deck.Count == 0) return null;
        var card = deck[Random.Range(0, deck.Count)];
        deck.Remove(card);
        return card;
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
}
