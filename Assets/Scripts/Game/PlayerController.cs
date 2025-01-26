using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private string userID;
    public int currentBalance;
    public int currentBet;
    public List<CardModel> cardsInHand = new List<CardModel> ();

    public List<Transform> cardSlots;
    public List<BetAction> getAvaiilableActions()
    {
        List<BetAction> availableActions = new List<BetAction>
        {
            BetAction.check,
            BetAction.fold,
            BetAction.call
        };

        // Add Raise and ReRaise if the balance allows
        if (currentBalance > currentBet)
        {
            availableActions.Add(BetAction.raise);
            availableActions.Add(BetAction.reRaise);
        }

        return availableActions;
    }


    public void Act(BetAction action, int newBet = 0)
    {
        switch (action)
        {
            case BetAction.check:
                Debug.Log("Player checked.");
                break;

            case BetAction.fold:
                Debug.Log("Player folded.");
                break;

            case BetAction.call:
                Debug.Log("Player called.");
                currentBalance -= currentBet;
                break;

            case BetAction.raise:
                Debug.Log($"Player raised with a new bet of {newBet}.");
                currentBet = newBet;
                currentBalance -= newBet;
                break;

            case BetAction.reRaise:
                Debug.Log($"Player re-raised with a new bet of {newBet}.");
                currentBet = newBet;
                currentBalance -= newBet;
                break;

            default:
                Debug.LogError("Invalid action.");
                break;
        }
    }

    private void Awake()
    {
        
        
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        foreach (CardModel card in cardsInHand)
        {
            GameObject cardObject = card.gameObject;
            foreach (Transform slot in cardSlots)
            {
                if (slot.childCount == 0)
                {
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
