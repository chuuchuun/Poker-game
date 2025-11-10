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
    public ulong playerId;
    private PlayerInput input;
    public List<ChipModel> totalChips = new List<ChipModel>();
    private RoundModel roundModel;
    private PopUpManager popupManager;
    private BetAction pendingAction;
    private int pendingBetAmount = 0;

    private List<ChipModel> blackChips = new List<ChipModel>();
    private List<ChipModel> redChips = new List<ChipModel>();
    private List<ChipModel> greenChips = new List<ChipModel>();
    private List<ChipModel> blueChips = new List<ChipModel>();

    private Transform chipBank;
    private Transform chipBankBlack;
    private Transform chipBankRed;
    private Transform chipBankGreen;
    private Transform chipBankBlue;

    private BalanceCanvasController balanceCanvasController;


    [SerializeField] private int currentBalance = 0;
    public int CurrentBalance
    {
        get => currentBalance;
        set
        {
            if (currentBalance != value)
            {
                int delta = value - currentBalance;
                currentBalance = value;

                if (IsServer)
                {
                    networkBalance.Value = currentBalance;
                    UpdateBalanceClientRpc(currentBalance);
                }

                if (delta > 0)
                {
                    AddChipsServerRpc(delta);
                }

                OnBalanceChanged?.Invoke(currentBalance);
            }
        }
    }

    public event Action<int> OnBalanceChanged;

    public int currentBet = 0;
    public List<CardModel> cardsInHand = new List<CardModel>();
    public List<Transform> cardSlots = new List<Transform>();

    private TMP_Text callText;
    public TMP_Text checkText;
    public TMP_Text foldText;
    public TMP_Text raiseText;
    public TMP_Text reraiseText;

    private bool isRoundStarted = false;
    private int spawnIndex = -1;
    private int chipCounter = 0;
    public NetworkVariable<bool> isMyTurn = new NetworkVariable<bool>();
    public NetworkVariable<int> networkBalance = new NetworkVariable<int>(0,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server);

    [SerializeField] private GameObject chipPrefabBlack;
    [SerializeField] private GameObject chipPrefabRed;
    [SerializeField] private GameObject chipPrefabGreen;
    [SerializeField] private GameObject chipPrefabBlue;
    [SerializeField] private GameObject balancePrefab;
    public override void OnNetworkSpawn()
    {
        playerId = OwnerClientId;

        Debug.Log($"Player {OwnerClientId} spawned - IsServer: {IsServer}, IsHost: {IsHost}, IsOwner: {IsOwner}");

        networkBalance.OnValueChanged += OnNetworkBalanceChanged;

        if (!IsOwner)
        {
            gameObject.SetActive(true);
        }

        if (IsServer)
        {
            Debug.Log($"Server assigning chips to player {OwnerClientId}");
            AssignChipsToPlayer();
        }
        else
        {
            Debug.Log($"Client {OwnerClientId} waiting to find chips");
            Invoke(nameof(FindMyChips), 0.5f);
        }
    }

    private void OnNetworkBalanceChanged(int oldValue, int newValue)
    {
        if (!IsServer)
        {
            Debug.Log($"Client {OwnerClientId} received network balance update: {oldValue} -> {newValue}");
            currentBalance = newValue;
            OnBalanceChanged?.Invoke(newValue);
        }
    }

    private void FindMyChips()
    {
        if (chipBankBlack == null || chipBankRed == null || chipBankGreen == null || chipBankBlue == null)
        {
            Debug.LogError("One or more chip bank references are missing!");
        }

        var allChips = FindObjectsOfType<ChipModel>();
        foreach (var chip in allChips)
        {
            if (chip.ownerClientId.Value == NetworkManager.Singleton.LocalClientId)
            {
                totalChips.Add(chip);
                currentBalance += chip.value;
                switch (chip.color)
                {
                    case ChipColor.black: blackChips.Add(chip); break;
                    case ChipColor.red: redChips.Add(chip); break;
                    case ChipColor.green: greenChips.Add(chip); break;
                    case ChipColor.blue: blueChips.Add(chip); break;
                }
            }
        }

        Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] Chips found: {totalChips.Count}");
    }

    private void AssignChipsToPlayer()
    {
        Debug.Log($"Assigning chips to player {OwnerClientId} - Current balance: {currentBalance}");

        totalChips.Clear();
        blackChips.Clear();
        redChips.Clear();
        greenChips.Clear();
        blueChips.Clear();
        currentBalance = 0;

        SpawnChips(ChipColor.black, 25, 4, chipPrefabBlack);
        SpawnChips(ChipColor.red, 10, 4, chipPrefabRed);
        SpawnChips(ChipColor.green, 5, 4, chipPrefabGreen);
        SpawnChips(ChipColor.blue, 1, 5, chipPrefabBlue);

        currentBalance = totalChips.Sum(c => c.value);
        if (IsServer)
        {
            networkBalance.Value = currentBalance;
        }

        Debug.Log($"Player {OwnerClientId} chips assigned. Total chips: {totalChips.Count}, Balance: {currentBalance}");
        OnBalanceChanged?.Invoke(currentBalance);
    }
    private int GenerateChipId()
    {
        if (IsServer)
        {
            return (int)(OwnerClientId * 1000) + chipCounter++;
        }
        return -1;
    }

    private void SpawnChips(ChipColor color, int value, int count, GameObject prefab)
    {
        Transform chipsParent = transform.Find("Chips/" + color.ToString());
        if (chipsParent == null)
        {
            Debug.LogError($"Missing Chips/{color} parent under player {OwnerClientId}.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            GameObject chipGO = Instantiate(prefab, chipsParent);
            chipGO.transform.localPosition = new Vector3(0, 0.005f * i, 0);

            var networkObject = chipGO.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.SpawnWithOwnership(OwnerClientId);
            }

            var chipModel = chipGO.GetComponent<ChipModel>();
            chipModel.value = value;
            chipModel.color = color;
            chipModel.ownerClientId.Value = OwnerClientId;

            if (IsServer)
            {
                chipModel.chipId = GenerateChipId();
                Debug.Log($"Spawned chip ID: {chipModel.chipId} for player {OwnerClientId}");
            }

            totalChips.Add(chipModel);
            currentBalance += chipModel.value;

            switch (color)
            {
                case ChipColor.black: blackChips.Add(chipModel); break;
                case ChipColor.red: redChips.Add(chipModel); break;
                case ChipColor.green: greenChips.Add(chipModel); break;
                case ChipColor.blue: blueChips.Add(chipModel); break;
            }

            Debug.Log($"Added {color} chip worth {value}. New balance: {currentBalance}");
        }
    }

    public void SetSpawnIndex(int index)
    {
        this.spawnIndex = index;
    }

    public int GetSpawnIndex()
    {
        return this.spawnIndex;
    }

    public List<BetAction> getAvailableActions()
    {
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

    public void Act(BetAction action, int newBet = 0)
    {
        Debug.Log($"Player {OwnerClientId} attempting action: {action}");

        if (roundModel == null)
        {
            roundModel = FindObjectOfType<RoundModel>();
            if (roundModel == null)
            {
                Debug.LogError("RoundModel not found!");
                return;
            }
        }

        if (action == BetAction.raise || action == BetAction.reRaise)
        {
            pendingAction = action;
            pendingBetAmount = newBet;
            popupManager.OpenPopup();
            return;
        }

        IPlayerAction playerAction = action switch
        {
            BetAction.check => new CheckAction(currentBet),
            BetAction.fold => new FoldAction(currentBet),
            BetAction.call => new CallAction(roundModel.GetCurrentHighestBet()),
            BetAction.raise => new RaiseAction(newBet),
            BetAction.reRaise => new ReRaiseAction(newBet),
            BetAction.start => new SkipAction(0),
            _ => new SkipAction(currentBet)
        };

        roundModel.TryToMakePlayerAction(playerAction);
    }

    private void HandleBetAmountSubmitted(int betAmount)
    {
        if (pendingAction == BetAction.raise || pendingAction == BetAction.reRaise)
        {
            int currentHighestBet = roundModel.GetCurrentHighestBet();
            int requiredToCall = currentHighestBet - currentBet;

            if (betAmount >= requiredToCall && betAmount <= currentBalance)
            {
                IPlayerAction playerAction = pendingAction switch
                {
                    BetAction.raise => new RaiseAction(betAmount),
                    BetAction.reRaise => new ReRaiseAction(betAmount),
                    _ => new SkipAction(currentBet)
                };

                roundModel.TryToMakePlayerAction(playerAction);
            }
            else
            {
                Debug.LogWarning($"Invalid bet amount: {betAmount}. Required: {requiredToCall}, Available: {currentBalance}");
                popupManager.OpenPopup();
            }
        }
        pendingAction = BetAction.start;
        pendingBetAmount = 0;
    }

    public void ClearHand()
    {
        if (!IsServer) return;

        var deckControl = GameManager.Instance.GetComponent<DeckControlBehavior>();
        foreach (CardModel card in cardsInHand)
        {
            deckControl.ReturnCard(card);
        }

        cardsInHand.Clear();
    }

    public List<ChipModel> RemoveChip(int bet)
    {
        List<ChipModel> chipsToRemove = new List<ChipModel>();
        int[] chipValues = new int[] { 25, 10, 5, 1 };

        foreach (int chipValue in chipValues)
        {
            while (bet > 0)
            {
                ChipModel chip = GetChipByValue(chipValue);
                if (chip != null)
                {
                    chipsToRemove.Add(chip);
                    switch (chip.color)
                    {
                        case ChipColor.black: blackChips.Remove(chip); break;
                        case ChipColor.red: redChips.Remove(chip); break;
                        case ChipColor.green: greenChips.Remove(chip); break;
                        case ChipColor.blue: blueChips.Remove(chip); break;
                    }
                    bet -= chipValue;
                }
                else
                {
                    break;
                }
            }
        }

        if (chipsToRemove.Count > 0)
        {
            var chipIds = chipsToRemove.Select(c => (ulong)c.chipId).ToArray();
            MoveChipsToBankServerRpc(chipIds);
            
            return chipsToRemove;
        }
        return null;
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

    [ServerRpc(RequireOwnership = false)]
    public void MoveChipsToBankServerRpc(ulong[] chipIds)
    {
        if (!IsServer) return;

        InitializeBank();

        var chipsByColor = totalChips
            .Where(c => chipIds.Contains((ulong)c.chipId))
            .GroupBy(c => c.color)
            .OrderBy(g => g.Key);

        foreach (var colorGroup in chipsByColor)
        {
            Transform targetParent = colorGroup.Key switch
            {
                ChipColor.black => chipBankBlack,
                ChipColor.red => chipBankRed,
                ChipColor.green => chipBankGreen,
                ChipColor.blue => chipBankBlue,
                _ => null
            };

            if (targetParent == null) continue;

            int stackBase = targetParent.childCount;
            int stackPosition = stackBase;

            foreach (var chip in colorGroup.OrderBy(c => c.stackPosition.Value))
            {
                chip.stackPosition.Value = stackPosition;
                chip.ownerClientId.Value = 0;
                chip.networkPosition.Value = new Vector3(0, stackPosition * 0.005f, 0);
                chip.parentNetworkId.Value = targetParent.GetComponent<NetworkObject>().NetworkObjectId;

                MoveChipToBank(chip, targetParent, stackPosition);

                UpdateChipPositionClientRpc(chip.chipId, targetParent.GetInstanceID(), stackPosition);

                stackPosition++;
            }
        }
    }

    [ClientRpc]
    private void UpdateChipPositionClientRpc(int chipId, int bankInstanceId, int stackPosition)
    {
        var chip = FindObjectsOfType<ChipModel>().FirstOrDefault(c => c.chipId == chipId);
        if (chip == null)
        {
            Debug.LogWarning($"Chip {chipId} not found on client");
            return;
        }

        var bank = FindObjectsOfType<Transform>().FirstOrDefault(t => t.GetInstanceID() == bankInstanceId);
        if (bank == null)
        {
            Debug.LogWarning($"Bank {bankInstanceId} not found on client");
            return;
        }

        chip.transform.SetParent(bank);
        chip.transform.localPosition = new Vector3(0, stackPosition * 0.005f, 0);
        chip.transform.localRotation = Quaternion.identity;
    }

    private void MoveChipToBank(ChipModel chip, Transform targetParent, int stackPosition)
    {
        if (!IsServer) return;

        chip.transform.SetParent(targetParent);
        chip.transform.localPosition = new Vector3(0, stackPosition * 0.005f, 0);
        chip.transform.localRotation = Quaternion.identity;
        totalChips.Remove(chip);

        currentBalance = totalChips.Sum(c => c.value);
        networkBalance.Value = currentBalance;
        UpdateBalanceClientRpc(currentBalance);

        chip.ownerClientId.Value = 0;
        chip.stackPosition.Value = stackPosition;

        Debug.Log($"Chip moved to bank. New balance: {currentBalance}");
    }

    private void InitializeBank()
    {
        if (chipBank == null)
        {
            chipBank = GameObject.FindGameObjectWithTag("chip_bank")?.transform;
            if (chipBank != null)
            {
                chipBankBlack = chipBank.Find("black");
                chipBankRed = chipBank.Find("red");
                chipBankGreen = chipBank.Find("green");
                chipBankBlue = chipBank.Find("blue");

                ClearBank();
            }
        }
    }

    [ClientRpc]
    public void UpdateBalanceClientRpc(int newBalance)
    {
        if (!IsServer) // Only update on clients
        {
            Debug.Log($"Client {OwnerClientId} received balance update: {newBalance}");
            currentBalance = newBalance;
            OnBalanceChanged?.Invoke(newBalance);
        }
    }

   
    private void ClearBank()
    {
        if (!IsServer) return;

        ClearBankColor(chipBankBlack);
        ClearBankColor(chipBankRed);
        ClearBankColor(chipBankGreen);
        ClearBankColor(chipBankBlue);
    }

    private void ClearBankColor(Transform colorBank)
    {
        if (colorBank == null) return;

        foreach (Transform child in colorBank)
        {
            if (child.TryGetComponent<ChipModel>(out var chip))
            {
                Destroy(chip.gameObject);
            }
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

    [ServerRpc(RequireOwnership = false)]
    public void AddChipsServerRpc(int amount)
    {
        if (!IsServer) return;

        int[] chipValues = new int[] { 25, 10, 5, 1 };

        foreach (int chipValue in chipValues)
        {
            while (amount >= chipValue)
            {
                SpawnChips(GetColorForValue(chipValue), chipValue, 1, GetPrefabForValue(chipValue));
                amount -= chipValue;
            }
        }
        currentBalance = totalChips.Sum(c => c.value);
        OnBalanceChanged?.Invoke(currentBalance);
    }

    public void AddChipsFromBank(List<ChipModel> chips)
    {
        foreach (var chip in chips)
        {
            chip.ownerClientId.Value = OwnerClientId;
            totalChips.Add(chip);

            switch (chip.color)
            {
                case ChipColor.black: blackChips.Add(chip); break;
                case ChipColor.red: redChips.Add(chip); break;
                case ChipColor.green: greenChips.Add(chip); break;
                case ChipColor.blue: blueChips.Add(chip); break;
            }

            currentBalance += chip.value;

            Transform targetParent = transform.Find("Chips/" + chip.color.ToString());
            chip.transform.SetParent(targetParent);
            chip.transform.localPosition = new Vector3(0, targetParent.childCount * 0.005f, 0);
            chip.transform.localRotation = Quaternion.identity;
        }

        Debug.Log($"Player {OwnerClientId} received {chips.Count} chips. New balance: {currentBalance}");
    }

    private ChipColor GetColorForValue(int value)
    {
        return value switch
        {
            25 => ChipColor.black,
            10 => ChipColor.red,
            5 => ChipColor.green,
            1 => ChipColor.blue,
            _ => ChipColor.blue
        };
    }

    private GameObject GetPrefabForValue(int value)
    {
        return value switch
        {
            25 => chipPrefabBlack,
            10 => chipPrefabRed,
            5 => chipPrefabGreen,
            1 => chipPrefabBlue,
            _ => chipPrefabBlue
        };
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
        OnBalanceChanged += balance =>
        {
            Debug.Log($"Balance updated to {balance}");
        };

        List<Transform> children = gameObject.GetComponentsInChildren<Transform>().ToList();
        foreach (Transform transform in children)
        {
            if (transform.CompareTag("hand_slot"))
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

        chipBank = GameObject.FindGameObjectWithTag("chip_bank")?.transform;
        if (chipBank != null)
        {
            chipBankBlack = chipBank.Find("black");
            chipBankRed = chipBank.Find("red");
            chipBankGreen = chipBank.Find("green");
            chipBankBlue = chipBank.Find("blue");
        }
        else
        {
            Debug.LogError("chip_bank object not found in the scene!");
        }
        roundModel = FindObjectOfType<RoundModel>();
        Debug.Log($"Player {gameObject.name} IsServer={IsServer} IsHost={NetworkManager.Singleton.IsHost} IsClient={IsClient}");
        popupManager = FindObjectOfType<PopUpManager>();
        if (popupManager is null)
        {
            Debug.LogError("popupManager object not found in the scene!");
        }
        else
        {
            PopUpManager.OnBetAmountSubmitted += HandleBetAmountSubmitted;
        }
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        InitializeBalanceCanvas();

        Debug.Log($"Player {OwnerClientId} Start - IsServer: {IsServer}, IsOwner: {IsOwner}, Balance: {currentBalance}, Chips: {totalChips.Count}");

        if (roundModel == null)
        {
            Debug.LogError("RoundModel not found in scene!");
        }

        if (!IsOwner)
        {
            AudioListener listener = GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = false;
            }
        }
        StartCoroutine(DelayedBalanceCheck());
    }

    private System.Collections.IEnumerator DelayedBalanceCheck()
    {
        yield return new WaitForSeconds(1f);

        int calculatedBalance = totalChips.Sum(c => c.value);
        if (calculatedBalance != currentBalance)
        {
            Debug.LogWarning($"Balance mismatch! Current: {currentBalance}, Calculated: {calculatedBalance}. Correcting...");
            currentBalance = calculatedBalance;
            OnBalanceChanged?.Invoke(currentBalance);
        }

        Debug.Log($"Player {OwnerClientId} final balance: {currentBalance}, total chips: {totalChips.Count}");
    }

    private void InitializeBalanceCanvas()
    {
        if (balancePrefab != null)
        {
            GameObject canvasInstance = Instantiate(balancePrefab, transform);
            balanceCanvasController = canvasInstance.GetComponent<BalanceCanvasController>();
            balanceCanvasController.Initialize(this);

            StartCoroutine(DelayedCanvasUpdate());
        }
        else
        {
            Debug.LogError("Balance prefab is not assigned!");
        }
    }

    private System.Collections.IEnumerator DelayedCanvasUpdate()
    {
        yield return new WaitForSeconds(0.5f);

        if (balanceCanvasController != null)
        {
            balanceCanvasController.UpdateBalanceDisplay(currentBalance);
            Debug.Log($"Canvas updated with balance: {currentBalance}");
        }
    }

    void InitializeChips()
    {
        foreach (ChipModel chip in totalChips)
        {
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
                    break;
            }
        }
    }

    private void DebugChipState()
    {
        Debug.Log($"=== Player {OwnerClientId} Chip State ===");
        Debug.Log($"Total Chips: {totalChips.Count}");
        Debug.Log($"Black Chips: {blackChips.Count}");
        Debug.Log($"Red Chips: {redChips.Count}");
        Debug.Log($"Green Chips: {greenChips.Count}");
        Debug.Log($"Blue Chips: {blueChips.Count}");
        Debug.Log($"Current Balance: {currentBalance}");
        Debug.Log($"Calculated Balance: {totalChips.Sum(c => c.value)}");
        Debug.Log($"IsServer: {IsServer}, IsOwner: {IsOwner}");
        Debug.Log($"=============================");
    }
    private void OnMouseEnter()
    {
        if (balanceCanvasController != null)
        {
            balanceCanvasController.OnMouseEnterPlayer();
        }
    }

    private void OnMouseExit()
    {
        if (balanceCanvasController != null)
        {
            balanceCanvasController.OnMouseExitPlayer();
        }
    }
    public void OnStart(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            LobbyController lobbyController = FindObjectOfType<LobbyController>();
            if (lobbyController != null && !lobbyController.HasStartedGame())
            {
                ulong userID = NetworkManager.Singleton.LocalClientId;
                lobbyController.ToggleReadiness();
                return;
            }
            InitializeChips();
            Act(BetAction.start);
        }
    }

    private void Update()
    {
        getAvailableActions();

        if (Input.GetKeyDown(KeyCode.C) && IsOwner)
        {
            DebugChipState();

            int calculated = totalChips.Sum(c => c.value);
            if (calculated != currentBalance)
            {
                currentBalance = calculated;
                OnBalanceChanged?.Invoke(currentBalance);
                Debug.Log($"Balance corrected to: {currentBalance}");
            }
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

    public void OnExit (InputAction.CallbackContext context)
    {
        Debug.Log("Exit action triggered");

        if (context.performed)
        {
            if (UIGameController.Instance != null)
                UIGameController.Instance.ToggleSettingsMenuVisibility();
        }
    }
}