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

    private NetworkVariable<int> _spawnIndex = new NetworkVariable<int>(-1);
    public int SpawnIndex => _spawnIndex.Value;

    // Replace isMyTurn with computed property
    public bool IsMyTurn => roundModel != null &&
                          roundModel.currentPlayerIndex.Value == _spawnIndex.Value;


    public int GetSpawnIndex()
    {
        return SpawnIndex;
    }

    public void SetSpawnIndex(int index)
    {
        if (!IsServer) return; 
        _spawnIndex.Value = index;
        Debug.Log($"Spawn index set to {index} for player {OwnerClientId}");
    }
    public List<BetAction> getAvailableActions()
    {
        Debug.Log($"Trying to get available actions for player {SpawnIndex}");
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
        if (!IsOwner) return;

        // Initialize roundModel reference securely
        StartCoroutine(WaitForRoundModelInitialization());
    }

    private IEnumerator WaitForRoundModelInitialization()
    {
        while (roundModel == null)
        {
            roundModel = FindObjectOfType<RoundModel>();
            yield return null;
        }

        if (IsServer)
        {
            _spawnIndex.Value = NetworkManager.Singleton.ConnectedClientsList.Count - 1;
            Debug.Log($"Server assigned spawn index: {_spawnIndex.Value}");
        }

        SetupNetworkVariableCallbacks();
    }

    private void SetupNetworkVariableCallbacks()
    {
        _spawnIndex.OnValueChanged += (oldVal, newVal) =>
        {
            Debug.Log($"SpawnIndex updated from {oldVal} to {newVal}");
            UpdateTurnState();
        };

        roundModel.currentPlayerIndex.OnValueChanged += (oldVal, newVal) =>
        {
            Debug.Log($"CurrentPlayerIndex updated from {oldVal} to {newVal}");
            UpdateTurnState();
        };
    }

    [ClientRpc]
    public void UpdateSpawnIndexClientRpc(int index)
    {
        if (IsServer) // Only server can actually change the value
        {
            _spawnIndex.Value = index;
        }
    }

    private void UpdateTurnState()
    {
        if (!IsOwner) return;

        Debug.Log($"Turn check - PlayerIndex: {roundModel.currentPlayerIndex.Value}, MyIndex: {_spawnIndex.Value}");

        if (IsMyTurn)
        {
            getAvailableActions();
        }
        else
        {
            ResetActionText();
        }
    }


    public void Act(BetAction action, int newBet = 0)
    {
        if (!IsOwner) return;

        Debug.Log($"Attempting {action} - IsMyTurn: {IsMyTurn}, SpawnIndex: {SpawnIndex}");

        if (!IsMyTurn && action != BetAction.start)
        {
            Debug.LogWarning($"Client thinks it's not their turn! Current: {roundModel.currentPlayerIndex.Value}, Mine: {SpawnIndex}");
            return;
        }

        if (action != BetAction.start)
        {
            ExecuteActionServerRpc(action, newBet);
        }
        else if (IsHost)
        {
            roundModel.StartGame();
            roundModel.NextRound();
            isRoundStarted = true;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ExecuteActionServerRpc(BetAction action, int newBet, ServerRpcParams rpcParams = default)
    {
        var senderClientId = rpcParams.Receive.SenderClientId;
        var playerObject = NetworkManager.Singleton.ConnectedClients[senderClientId].PlayerObject;
        var playerController = playerObject.GetComponent<PlayerController>();

        if (roundModel.currentPlayerIndex.Value != playerController.SpawnIndex)
        {
            Debug.LogWarning($"Server rejected action - Not player's turn! Current: {roundModel.currentPlayerIndex.Value}, Theirs: {playerController.SpawnIndex}");
            return;
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
            default:
                Debug.LogError("Invalid action.");
                return;
        }
        roundModel.NextRound();
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
        if (IsOwner) {
            //Debug.Log($"My spawn index is {SpawnIndex} and my turn is {IsMyTurn}");
        }
        //if (isRoundStarted)
        //{
            //bool isNowMyTurn = (roundModel.currentPlayerIndex.Value == spawnIndex.Value);
            //if (isMyTurn != isNowMyTurn)
            //{
              //  isMyTurn = isNowMyTurn;
               // UpdateTurnState();
            //}
        //}
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
        if (!context.performed || !IsOwner) return;
        Debug.Log($"Call attempt - Valid: {IsMyTurn}, Index: {_spawnIndex.Value}");
        Act(BetAction.call);
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
