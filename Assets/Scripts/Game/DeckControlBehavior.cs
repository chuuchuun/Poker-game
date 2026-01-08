using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class DeckControlBehavior : NetworkBehaviour
{
    public NetworkList<NetworkObjectReference> cardsOnTable;

    private NetworkList<NetworkObjectReference> deck;
    private GameObject[] cardSlots;
    private GameObject[] foldSlots;
    private IPlayerController[] players;

    private void Awake()
    {
        deck = new NetworkList<NetworkObjectReference>();
        cardsOnTable = new NetworkList<NetworkObjectReference>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            InitializeDeckAndSlots();
        }
    }

    public override void OnDestroy()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        }

        deck?.Dispose();
        cardsOnTable?.Dispose();
    }

    private void OnClientConnected(ulong clientId)
    {
        CollectAllCardsServerRpc();
    }

    public List<CardModel> GetCardsOnTable()
    {
        List<CardModel> deckCards = new List<CardModel>();

        foreach (var cardRef in cardsOnTable)
        {
            if (cardRef.TryGet(out NetworkObject netObj))
            {
                CardModel card = netObj.GetComponent<CardModel>();
                if (card != null)
                {
                    deckCards.Add(card);
                }
            }
        }

        return deckCards;
    }

    public void InitializeDeckAndSlots()
    {
        if (!IsServer) return;

        deck.Clear();
        CardModel[] allCards = FindObjectsOfType<CardModel>();
        for (int i = 0; i < allCards.Length; i++)
        {
            CardModel card = allCards[i];
            if (card.NetworkObject.IsSpawned)
            {
                deck.Add(new NetworkObjectReference(card.NetworkObject));
                ResetCardPosition(card.NetworkObject, 0.001f * i);
            }
        }

        cardSlots = GameObject
            .FindGameObjectsWithTag("slot")
            .OrderBy(slot =>
            {
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

    public void DealCards(List<IPlayerController> playerModels)
    {
        if (!IsServer) return;

        players = playerModels.ToArray();

        foreach (var player in playerModels.Where(p => p.IsSpawned))
        {
            DealToPlayer(player);
        }
    }

    private void DealToPlayer(IPlayerController player)
    {
        if (!IsServer) return;

        int cardsNeeded = 2 - player.CardsInHand.Count;

        for (int i = 0; i < cardsNeeded; i++)
        {
            if (deck.Count == 0) break;

            NetworkObjectReference cardRef = deck[Random.Range(0, deck.Count)];
            if (cardRef.TryGet(out NetworkObject cardObject))
            {
                CardModel card = cardObject.GetComponent<CardModel>();
                player.CardsInHand.Add(card);
                deck.Remove(cardRef);

                UpdateCardPositionClientRpc(
                    cardRef,
                    new NetworkObjectReference(player.NetworkObject),
                    i,
                    true
                );
            }
        }
    }

    public void CollectAllCards()
    {
        CollectAllCardsServerRpc();
    }

    [ServerRpc]
    private void CollectAllCardsServerRpc()
    {
        if (!IsServer) return;

        deck.Clear();
        cardsOnTable.Clear();

        CardModel[] allCards = FindObjectsOfType<CardModel>();
        GameObject deckObject = GameObject.FindGameObjectWithTag("deck");

        for (int i = 0; i < allCards.Length; i++)
        {
            CardModel card = allCards[i];
            if (card.NetworkObject.IsSpawned)
            {
                deck.Add(new NetworkObjectReference(card.NetworkObject));
                ResetCardPositionClientRpc(new NetworkObjectReference(card.NetworkObject), 0.001f * i);
            }
        }

        foreach (IPlayerController player in players)
        {
            player.CardsInHand.Clear();
        }
    }

    [ClientRpc]
    private void ResetCardPositionClientRpc(NetworkObjectReference cardRef, float heightInDeck)
    {
        if (cardRef.TryGet(out NetworkObject cardObject))
        {
            ResetCardPosition(cardObject, heightInDeck);
        }
    }

    private void ResetCardPosition(NetworkObject cardObject, float heightInDeck)
    {
        GameObject deckObject = GameObject.FindGameObjectWithTag("deck");
        cardObject.transform.SetParent(deckObject.transform);
        cardObject.transform.localPosition = new Vector3(0, heightInDeck, 0);
        cardObject.transform.localRotation = Quaternion.Euler(90f, 0, 0);

        if (cardObject.TryGetComponent<NetworkTransform>(out var netTransform))
        {
            netTransform.Teleport(
                cardObject.transform.position,
                cardObject.transform.rotation,
                cardObject.transform.localScale
            );
        }
    }

    public bool CanAddCardsToTable()
    {
        return cardsOnTable.Count < 5;
    }

    [ServerRpc]
    public void AddCardsToTableServerRpc()
    {
        if (!IsServer) return;

        int countToDeal;
        switch (cardsOnTable.Count)
        {
            case 0:
                countToDeal = 3;
                break;
            case 3:
            case 4:
                countToDeal = 1;
                break;
            default:
                countToDeal = 0;
                break;
        }

        if (countToDeal == 0) return;

        for (int i = 0; i < countToDeal; i++)
        {
            if (deck.Count == 0) break;

            NetworkObjectReference cardRef = deck[Random.Range(0, deck.Count)];
            if (cardRef.TryGet(out NetworkObject cardObject))
            {
                cardsOnTable.Add(cardRef);
                deck.Remove(cardRef);
                PlaceCardOnTableClientRpc(cardRef, cardsOnTable.Count - 1);
            }
        }
    }

    [ClientRpc]
    private void PlaceCardOnTableClientRpc(NetworkObjectReference cardRef, int slotIndex)
    {
        if (cardRef.TryGet(out NetworkObject cardObject) && slotIndex < cardSlots.Length)
        {
            Transform slotTransform = cardSlots[slotIndex].transform;
            cardObject.transform.SetParent(slotTransform);
            cardObject.transform.localPosition = Vector3.zero;
            cardObject.transform.localRotation = Quaternion.Euler(-90f, 0, 0);

            if (cardObject.TryGetComponent<NetworkTransform>(out var netTransform))
            {
                netTransform.Teleport(
                    cardObject.transform.position,
                    cardObject.transform.rotation,
                    cardObject.transform.localScale
                );
            }
        }
    }

    public void ReturnCard(CardModel card)
    {
        IPlayerController player = FindObjectsOfType<MonoBehaviour>()
            .OfType<IPlayerController>()
            .FirstOrDefault(p => p.CardsInHand.Contains(card));

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
                    card.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
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
    private void UpdateCardPositionClientRpc(NetworkObjectReference cardRef, NetworkObjectReference playerRef, int slotIndex, bool isFaceDown)
    {
        if (cardRef.TryGet(out NetworkObject cardObject) && playerRef.TryGet(out NetworkObject playerObject))
        {
            IPlayerController player = playerObject.GetComponents<MonoBehaviour>().OfType<IPlayerController>().FirstOrDefault();
            if (player != null && slotIndex < player.CardSlots.Count)
            {
                Transform slot = player.CardSlots[slotIndex];
                cardObject.transform.SetParent(slot);
                cardObject.transform.position = slot.position;
                var addRotation = isFaceDown ? Quaternion.Euler(0, 180f, 0) : Quaternion.identity;
                cardObject.transform.rotation = slot.rotation * addRotation;

                if (cardObject.TryGetComponent<NetworkTransform>(out var netTransform))
                {
                    netTransform.Teleport(
                        cardObject.transform.position,
                        cardObject.transform.rotation,
                        cardObject.transform.localScale
                    );
                }
            }
        }
    }
}