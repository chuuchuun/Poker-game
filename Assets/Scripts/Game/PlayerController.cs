using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private string userID;
    private PlayerInput input;
    public List<ChipModel> totalChips = new List<ChipModel>();
    
    private List<ChipModel> blackChips = new List<ChipModel>();
    private List<ChipModel> redChips = new List<ChipModel>();
    private List<ChipModel> greenChips = new List<ChipModel>();
    private List<ChipModel> blueChips = new List<ChipModel>();

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
                RemoveChip(newBet);
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

    void RemoveChip(int bet)
    {
        List<ChipModel> chipsToRemove = new List<ChipModel>();

        int[] chipValues = new int[] { 25, 10, 5, 1 };

        foreach (int chipValue in chipValues)
        {
            while (bet >= chipValue)
            {
                ChipModel chip = GetChipByValue(chipValue);
                if (chip != null)
                {
                    chipsToRemove.Add(chip);
                    bet -= chipValue;

                    totalChips.Remove(chip);
                    RemoveFromColorList(chip);
                }
                else
                {
                    break;
                }
            }
        }

        if (bet > 0)
        {
            Debug.LogWarning("Unable to remove exact chip value for bet.");
        }
        else
        {
            Debug.Log($"Removed chips for bet: {string.Join(", ", chipsToRemove.Select(c => c.value))}");
        }
    }

    ChipModel GetChipByValue(int value)
    {
        switch (value)
        {
            case 25:
                return blackChips.FirstOrDefault();
            case 10:
                return redChips.FirstOrDefault();
            case 5:
                return greenChips.FirstOrDefault();
            case 1:
                return blueChips.FirstOrDefault();
            default:
                return null;
        }
    }

    void RemoveFromColorList(ChipModel chip)
    {
        GameObject chipModel = chip.GameObject();
        chipModel.SetActive(false);
        switch (chip.color)
        {
            case ChipColor.black:
                blackChips.Remove(chip);
                break;
            case ChipColor.red:
                redChips.Remove(chip);
                break;
            case ChipColor.green:
                greenChips.Remove(chip);
                break;
            case ChipColor.blue:
                blueChips.Remove(chip);
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

        PopulateChipsList();
    }

    void PopulateChipsList()
    {
        totalChips.Clear();
        blackChips.Clear();
        redChips.Clear();
        greenChips.Clear();
        blueChips.Clear();

        Transform chipsParent = transform.Find("Chips");
        if (chipsParent == null)
        {
            Debug.LogError("No 'Chips' object found under the player.");
            return;
        }

        foreach (Transform colorCategory in chipsParent)
        {
            foreach (Transform chipTransform in colorCategory)
            {
                if (chipTransform.CompareTag("chip"))
                {
                    ChipModel chip = chipTransform.GetComponent<ChipModel>();
                    if (chip != null)
                    {
                        totalChips.Add(chip);

                        switch (chip.color)
                        {
                            case ChipColor.black:
                                blackChips.Add(chip);
                                break;
                            case ChipColor.red:
                                redChips.Add(chip);
                                break;
                            case ChipColor.green:
                                greenChips.Add(chip);
                                break;
                            case ChipColor.blue:
                                blueChips.Add(chip);
                                break;
                            default:
                                Debug.LogWarning("Unknown chip color: " + chip.color);
                                break;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Chip GameObject {chipTransform.name} is missing ChipModel component.");
                    }
                }
            }
        }

        Debug.Log($"Chips populated. Total chips: {totalChips.Count}");
    }



    void Start()
    {
        foreach(ChipModel chip in totalChips)
        {
            currentBalance += chip.value;
            switch (chip.color) {
                case ChipColor.black:
                    blackChips.Add(chip);
                    break;
                case ChipColor.red: 
                    redChips.Add(chip); 
                    break;
                case ChipColor.green:
                    greenChips.Add(chip);
                    break;
                case ChipColor.blue:
                    blueChips.Add(chip);
                    break;
                default:
                    break;
            }
        }
    }

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
