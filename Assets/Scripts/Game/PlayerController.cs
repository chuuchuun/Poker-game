using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private string userID;
    private PlayerInput input;
    public int currentBalance = 200;
    public int currentBet = 0;
    public List<CardModel> cardsInHand = new List<CardModel> ();
    public List<Transform> cardSlots;
    
    public TMP_Text callText;
    public TMP_Text checkText;
    public TMP_Text foldText;
    public TMP_Text raiseText;
    public TMP_Text reraiseText;


    public List<BetAction> getAvailableActions()
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
        foreach (BetAction action in availableActions)
        {
            switch (action)
            {
                case BetAction.check:
                    checkText.enabled = true;
                    break;

                case BetAction.fold:
                    foldText.enabled = true;
                    break;

                case BetAction.call:
                    callText.enabled = true;
                    break;

                case BetAction.raise:
                    raiseText.enabled = true;
                    break;

                case BetAction.reRaise:
                   reraiseText.enabled = true;
                    break;

                default:
                    break;
            }
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
        callText.enabled = false;
        checkText.enabled = false;
        foldText.enabled = false;
        raiseText.enabled = false;
        reraiseText.enabled = false;

        input = GetComponent<PlayerInput>();
        if (input != null)
        {
            Debug.Log("Got input", input);
        }
        else
        {
            Debug.Log("something went wrong :(");
        }
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
                    cardObject.transform.Rotate(30f, 180f, 0);
                    cardObject.transform.SetParent(slot);
                    cardObject.transform.localPosition = Vector3.zero;
                    Debug.Log("Assigned card to slot: " + slot.name);
                    break;
                }
            }
        }

        getAvailableActions();


    }



    public void OnCall(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Act(BetAction.call);
        }
    }

    public void OnFold(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Act(BetAction.fold);
        }
    }

    public void OnCheck(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Act(BetAction.check);
        }
    }

    public void OnRaise(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Act(BetAction.raise, 50);
        }
    }

    public void OnReraise(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Act(BetAction.reRaise, 100); 
        }
    }

}
