using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    private RoundModel roundModel;
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
    

    private TMP_Text callText;
    public TMP_Text checkText;
    public TMP_Text foldText;
    public TMP_Text raiseText;
    public TMP_Text reraiseText;


    private bool isRoundStarted = false;
    private int spawnIndex = -1;
    public bool isMyTurn = false;


    public int GetSpawnIndex()
    {
        return this.spawnIndex;
    }

    public List<BetAction> getAvailableActions()
    {
        //Debug.Log($"Trying to get available actions for player {this.spawnIndex}");
        ResetActionText();
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
        else
        {
            availableActions.Remove(BetAction.raise);
            availableActions.Remove(BetAction.reRaise);
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

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            gameObject.SetActive(true);
        }

        if (IsServer)
        {
            spawnIndex = (int)OwnerClientId;
        }
        roundModel.currentPlayerIndex.OnValueChanged += (oldValue, newValue) => UpdateTurnState();
        UpdateTurnState();
    }

    public void SetSpawnIndex(int index)
    {
        this.spawnIndex = index;
    }

    private void UpdateTurnState()
    {
        bool isNowMyTurn = (roundModel.currentPlayerIndex.Value == spawnIndex);
        isMyTurn = isNowMyTurn;
        UpdateUIForTurnClientRpc(isNowMyTurn);
    }

    [ClientRpc]
    public void UpdateUIForTurnClientRpc(bool isMyTurn)
    {
        Debug.Log($"Updating UI for Player {spawnIndex}. Is my turn? {isMyTurn}");
        if (isMyTurn)
        {
            getAvailableActions(); // Enable buttons
        }
        else
        {
            ResetActionText(); // Disable buttons
        }
    }

    public void Act(BetAction action, int newBet = 0)
    {
        if (!isMyTurn && action != BetAction.start)
        {
            Debug.Log($"Not turn of player {this.spawnIndex}");
            return; // Ignore input if it's not this player's turn
        }
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
                break;
            case BetAction.raise:
                Debug.Log($"Player raised {newBet}.");
                break;
            case BetAction.reRaise:
                Debug.Log($"Player re-raised {newBet}.");
                break;
            case BetAction.start:
                if (IsHost && !isRoundStarted)
                {
                    roundModel.StartGame();
                    isRoundStarted = true;
                }
                break;
            default:
                Debug.LogError("Invalid action.");
                return;
        }

        ResetActionText();
        StartCoroutine(AdvanceTurnWithDelay());
    }

    IEnumerator AdvanceTurnWithDelay()
    {
        yield return new WaitForSeconds(2);
        if (IsServer)
        {
            roundModel.NextRound(); // Server moves turn to the next player
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

    void PopulateActionTexts()
    {
        List<TMP_Text> texts = FindObjectsOfType<TMP_Text>().ToList();
        foreach (TMP_Text text in texts)
        {
            switch (text.tag)
            {
                case "call_text":
                    callText = text;
                    break;
                case "check_text":
                    checkText = text;
                    break;
                case "fold_text":
                    foldText = text;
                    break;
                case "raise_text":
                    raiseText = text;
                    break;
                case "reraise_text":
                    reraiseText = text;
                    break;
            }
        }
        ResetActionText();
        
    }

    void ResetActionText()
    {
        callText.enabled = false;
        checkText.enabled = false;
        foldText.enabled = false;
        raiseText.enabled = false;
        reraiseText.enabled = false;
    }
    private void Awake()
    {
        this.roundModel = FindObjectsOfType<RoundModel>()[0];

        List<Transform> children = gameObject.GetComponentsInChildren<Transform>().ToList();
        foreach(Transform transform in children)
        {
            if (transform.CompareTag("slot"))
            {
                cardSlots.Add(transform);
                Debug.Log($"aDDED CARD SLOT {transform.name}");
            }
        }
        PopulateActionTexts();
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
        //Debug.Log($"{this.spawnIndex} is {isMyTurn}");

        if (isRoundStarted)
        {
            // Show action texts only if it's the player's turn
            if (this.isMyTurn)
            {
                getAvailableActions();
            }
            else
            {
                ResetActionText();
            }
        }
    }




    public void OnStart(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Act(BetAction.start);
        }
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
