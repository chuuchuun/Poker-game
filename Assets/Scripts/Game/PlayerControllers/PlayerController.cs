using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerController : NetworkBehaviour, IPlayerController
{
    QueueControlBehavior queueControlBehavior => GetComponent<QueueControlBehavior>();
    public ulong PlayerId => playerId;
    public int CurrentBet
    {
        get => currentBet;
        set => currentBet = value;
    }

    public List<CardModel> CardsInHand => cardsInHand;
    public List<Transform> CardSlots => cardSlots;

    public List<ChipModel> TotalChips => totalChips;
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

    private bool isHidden = false;

    [SerializeField] private int currentBalance = 0;
    public int CurrentBalance
    {
        get => currentBalance;
        set
        {
            if (currentBalance != value)
            {
                currentBalance = value;

                if (IsServer)
                {
                    networkBalance.Value = currentBalance;
                    UpdateBalanceClientRpc(currentBalance);

                    RespawnChipsForBalanceServer();
                }

                OnBalanceChanged?.Invoke(currentBalance);
            }
        }
    }

    public event Action<int> OnBalanceChanged;

    public void RequestSetBalance(int newBalance)
    {
        if (IsServer)
        {
            CurrentBalance = newBalance;
            return;
        }

        RequestSetBalanceServerRpc(newBalance);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetBalanceServerRpc(int newBalance)
    {
        if (!IsServer) return;
        CurrentBalance = newBalance;
    }

    public int currentBet = 0;
    public List<CardModel> cardsInHand = new List<CardModel>();
    public List<Transform> cardSlots = new List<Transform>();

    private TMP_Text callText;
    private TMP_Text checkText;
    private TMP_Text foldText;
    private TMP_Text raiseText;
    private TMP_Text reraiseText;
    private TMP_Text chipsText;

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

    private bool isExitingToMenu;

    public override void OnNetworkSpawn()
    {
        currentBet = 0;
        pendingAction = BetAction.start;
        pendingBetAmount = 0;
        chipCounter = 0;
        cardsInHand.Clear();
        totalChips.Clear();
        blackChips.Clear();
        redChips.Clear();   
        greenChips.Clear();
        blueChips.Clear();  
        currentBalance = 0;

        playerId = OwnerClientId;

        if (IsOwner && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnLocalClientDisconnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnLocalClientDisconnected;
        }

        Debug.Log($"Player {OwnerClientId} spawned - IsServer: {IsServer}, IsHost: {IsHost}, IsOwner: {IsOwner}");

        networkBalance.OnValueChanged += OnNetworkBalanceChanged;
        isMyTurn.OnValueChanged += OnIsMyTurnChanged;

        if (PlayerId == NetworkManager.Singleton.LocalClientId)
        {
            if (isMyTurn.Value)
                GetAvailableActions();
            else
                ResetActionText();
        }

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

    public override void OnDestroy()
    {
        if (IsOwner && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnLocalClientDisconnected;
        }
    }

    private void OnLocalClientDisconnected(ulong clientId)
    {
        if (isExitingToMenu) return;
        if (NetworkManager.Singleton == null) return;
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        isExitingToMenu = true;

        NetworkManager.Singleton?.Shutdown();

        if (GameManager.Instance != null && GameManager.Instance.IsSingleplayer)
        {
            SceneManager.LoadScene("ModeSelectionScreen");
        }
        else
        {
            SceneManager.LoadScene("MultiplayerScreen");
        }

        Cursor.lockState = CursorLockMode.None;
    }

    private void OnNetworkBalanceChanged(int oldValue, int newValue)
    {
        if (!IsServer)
        {
            Debug.Log($"Client {OwnerClientId} received network balance update: {oldValue} -> {newValue}");
            currentBalance = newValue;
            OnBalanceChanged?.Invoke(newValue);
        }

        if (PlayerId == NetworkManager.Singleton.LocalClientId)
        {
            chipsText.text = $"Balance: {currentBalance}";
        }

    }

    private void OnIsMyTurnChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"PlayerController.OnIsMyTurnChanged: Player {PlayerId} {oldValue} -> {newValue} (LocalClient={NetworkManager.Singleton.LocalClientId})");

        if (PlayerId == NetworkManager.Singleton.LocalClientId)
        {
            if (newValue)
            {
                GetAvailableActions();
            }
            else
            {
                ResetActionText();
            }
        }
    }

    private void FindMyChips()
    {
        if (chipBankBlack == null || chipBankRed == null || chipBankGreen == null || chipBankBlue == null)
        {
            Debug.LogError("One or more chip bank references are missing!");
        }

        totalChips.Clear();
        blackChips.Clear();
        redChips.Clear();
        greenChips.Clear();
        blueChips.Clear();
        currentBalance = 0;

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

        RequestSetBalance(165);

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

    private void RespawnChipsForBalanceServer()
    {
        if (!IsServer) return;

        DespawnAllPlayerChipsServer();
        SpawnChipsForBalanceServer(currentBalance);
    }

    private void DespawnAllPlayerChipsServer()
    {
        if (!IsServer) return;

        var chipsToDespawn = totalChips.Where(c => c != null).ToList();
        foreach (var chip in chipsToDespawn)
        {
            var netObj = chip.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn(true);
            else
                Destroy(chip.gameObject);
        }

        totalChips.Clear();
        blackChips.Clear();
        redChips.Clear();
        greenChips.Clear();
        blueChips.Clear();
    }

    private void SpawnChipsForBalanceServer(int balance)
    {
        if (!IsServer) return;

        int remaining = Mathf.Max(0, balance);
        chipCounter = 0;

        int[] chipValues = new int[] { 25, 10, 5, 1 };
        foreach (int chipValue in chipValues)
        {
            while (remaining >= chipValue)
            {
                SpawnChips(GetColorForValue(chipValue), chipValue, 1, GetPrefabForValue(chipValue));
                remaining -= chipValue;
            }
        }

        currentBalance = totalChips.Sum(c => c.value);
        networkBalance.Value = currentBalance;
        UpdateBalanceClientRpc(currentBalance);
        OnBalanceChanged?.Invoke(currentBalance);
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
            chipGO.transform.localPosition = new Vector3(0, 0.2f + 0.005f * i, 0);

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

    public List<BetAction> GetAvailableActions()
    {
        ResetActionText();
        if (!isMyTurn.Value) return new List<BetAction>();
        List<BetAction> availableActions = new List<BetAction>
        {
            BetAction.fold,
        };

        var requiredToCall = roundModel.GetCurrentHighestBet() - CurrentBet;

        if (requiredToCall > 0)
        {
            availableActions.Add(BetAction.reRaise);
            availableActions.Add(BetAction.call);
        }
        else
        {
            availableActions.Add(BetAction.check);
            availableActions.Add(BetAction.raise);
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
            BetAction.check => new CheckAction(currentBet, this),
            BetAction.fold => new FoldAction(currentBet, this),
            BetAction.call => new CallAction(roundModel.GetCurrentHighestBet(), this),
            BetAction.raise => new RaiseAction(newBet, this),
            BetAction.reRaise => new ReRaiseAction(newBet, this),
            BetAction.start => new SkipAction(0, this),
            _ => new SkipAction(currentBet, this)
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
                    BetAction.raise => new RaiseAction(betAmount, this),
                    BetAction.reRaise => new ReRaiseAction(betAmount, this),
                    _ => new SkipAction(currentBet, this)
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

    private int[] RequiredChips(int bet)
    {
        int[] chipValues = new int[] { 25, 10, 5, 1 };
        List<int> requiredChipValues = new List<int>();
        for (int i = 0; i < chipValues.Length; i++)
        {
            if (bet <= 0)
                break;

            int chipValue = chipValues[i];
            int maxChipsNeeded = bet / chipValue;
            int chipsUsed = maxChipsNeeded;

            for (int j = 0; j < chipsUsed; j++)
                requiredChipValues.Add(chipValue);

            bet -= chipsUsed * chipValue;
        }

        return requiredChipValues.ToArray();
    }

    public List<ChipModel> RemoveChip(int bet)
    {
        if (bet <= 0) return new List<ChipModel>();

        int betToRemove = Mathf.Clamp(bet, 0, currentBalance);
        if (betToRemove == 0) return new List<ChipModel>();
        List<ChipModel> movedToBank = RequiredChips(bet)
            .Select(value => CreateChipForColor(value)).ToList();

        if (IsServer)
        {
            CurrentBalance = currentBalance - betToRemove;
        }
        else
        {
            RequestSetBalance(currentBalance - betToRemove);
        }

        return movedToBank;
    }

    ChipModel CreateChipForColor(int value)
    {
        var prefab = GetColorForValue(value) switch
        {
            ChipColor.black => chipPrefabBlack,
            ChipColor.red => chipPrefabRed,
            ChipColor.green => chipPrefabGreen,
            ChipColor.blue => chipPrefabBlue,
            _ => chipPrefabBlue
        };

        var parent = GetColorForValue(value) switch
        {
            ChipColor.black => chipBankBlack,
            ChipColor.red => chipBankRed,
            ChipColor.green => chipBankGreen,
            ChipColor.blue => chipBankBlue,
            _ => chipBankBlue
        };

        GameObject chipGO = Instantiate(prefab, chipBank);
        chipGO.transform.localPosition = new Vector3(0, 0.02f, 0);

        var networkObject = chipGO.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.SpawnWithOwnership(OwnerClientId);
        }

        var chipModel = chipGO.GetComponent<ChipModel>();
        chipModel.value = value;
        chipModel.color = GetColorForValue(value);
        chipModel.ownerClientId.Value = OwnerClientId;

        if (IsServer)
        {
            chipModel.chipId = GenerateChipId();
            Debug.Log($"Spawned chip ID: {chipModel.chipId} for player {OwnerClientId}");
        }

         return chipModel;
    }

    [ServerRpc(RequireOwnership = false)]
    public void MoveChipsToBankServerRpc(ulong[] chipIds)
    {
        if (!IsServer) return;

        InitializeBank();

        var chipsToRemove = totalChips
            .Where(chip => chipIds.ToList().Contains((ulong)chip.chipId))
            .ToList();

        foreach (var chip in chipsToRemove)
        {
            Transform targetParent = chip.color switch
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

            chip.stackPosition.Value = stackPosition;
            chip.ownerClientId.Value = 0;
            chip.networkPosition.Value = new Vector3(0, stackPosition * 0.1f, 0);
            if (targetParent.GetComponent<NetworkObject>() != null)
                chip.parentNetworkId.Value = targetParent.GetComponent<NetworkObject>().NetworkObjectId;
            else
                chip.parentNetworkId.Value = 0;

            MoveChipToBank(chip, targetParent, stackPosition);

            UpdateChipPositionClientRpc(chip.chipId, (int)chip.color, stackPosition);
        }
    }

    [ClientRpc]
    private void UpdateChipPositionClientRpc(int chipId, int colorIndex, int stackPosition)
    {
        var chip = FindObjectsOfType<ChipModel>().FirstOrDefault(c => c.chipId == chipId);
        if (chip == null)
        {
            Debug.LogWarning($"Chip {chipId} not found on client");
            return;
        }

        var bank = GameObject.FindGameObjectWithTag("chip_bank")?.transform;
        if (bank == null)
        {
            Debug.LogWarning($"chip_bank not found on client");
            return;
        }

        string colorName = ((ChipColor)colorIndex).ToString();
        var colorParent = bank.Find(colorName);
        if (colorParent == null)
        {
            Debug.LogWarning($"Bank color '{colorName}' not found under chip_bank on client");
            return;
        }

        chip.transform.SetParent(colorParent);
        chip.transform.localPosition = new Vector3(0, stackPosition * 0.005f, 0);
        chip.transform.localRotation = Quaternion.identity;
    }

    private void MoveChipToBank(ChipModel chip, Transform targetParent, int stackPosition)
    {
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
        if (!IsServer)
        {
            Debug.Log($"Client {OwnerClientId} received balance update: {newBalance}");
            currentBalance = newBalance;
            OnBalanceChanged?.Invoke(newBalance);
        }
    }

    [ClientRpc]
    public void UpdateTurnClientRpc(ulong targetPlayerId, bool myTurn)
    {
        PlayerController pc = null;
        if (NetworkManager.Singleton != null)
        {
            var netObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(targetPlayerId);
            if (netObj != null)
                pc = netObj.GetComponent<PlayerController>();
        }

        if (pc == null)
            pc = FindObjectsOfType<PlayerController>().FirstOrDefault(p => p.PlayerId == targetPlayerId);

        if (pc == null)
        {
            Debug.LogWarning($"UpdateTurnClientRpc: player {targetPlayerId} not found on client.");
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == targetPlayerId)
        {
            if (myTurn)
                pc.GetAvailableActions();
            else
                pc.ResetActionText();
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
                case "chips_text":
                    chipsText = text;
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
        if(context.performed)
        {
            LobbyController lobbyController = FindObjectOfType<LobbyController>();
            if (lobbyController != null && !lobbyController.HasStartedGame())
            {
                lobbyController.ToggleReadiness();
                return;
            }
            InitializeChips();
            Act(BetAction.start);
        }
    }

    private void Update()
    {
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

    public void NotifyTurn(bool myTurn)
    {
        if (IsServer)
        {
            isMyTurn.Value = myTurn;
        }

        if (IsOwner) GetAvailableActions();
    }

    public void SetModelVisibility(bool visible)
    {
        if (isHidden == !visible) return;

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r.GetComponent<Camera>() != null) continue;
            r.enabled = visible;
        }

        var canvases = GetComponentsInChildren<Canvas>(true);
        foreach (var c in canvases)
        {
            c.enabled = visible;
        }

        var texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts)
        {
            t.enabled = visible;
        }

        var colliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            col.enabled = visible;
        }

        if (IsOwner && input != null)
        {
            input.enabled = visible;
        }

        isHidden = !visible;
    }

    public void Kick()
    {
        Debug.Log($"Kick requested for player {PlayerId} (IsServer={IsServer}, IsOwner={IsOwner})");

        if (IsServer)
        {
            try
            {
                var round = GameManager.Instance?.GetComponent<RoundModel>();
                if (round != null)
                {
                    round.RemovePlayerById(PlayerId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to remove player references before hide: {ex.Message}");
            }

            HidePlayerClientRpc(PlayerId);

            Debug.Log($"Player {PlayerId} hidden by server (no disconnect).");
            return;
        }

        RequestHidePlayerServerRpc();
        SetModelVisibility(false);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestHidePlayerServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        try
        {
            var round = GameManager.Instance?.GetComponent<RoundModel>();
            if (round != null)
            {
                round.RemovePlayerById(senderId);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to remove player references before hide (server RPC): {ex.Message}");
        }

        HidePlayerClientRpc(senderId);

        Debug.Log($"Server received hide request and hid player {senderId} (no disconnect).");
    }

    [ClientRpc]
    private void HidePlayerClientRpc(ulong targetPlayerId)
    {
        NetworkObject netObj = null;
        if (NetworkManager.Singleton != null)
        {
            netObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(targetPlayerId);
        }

        PlayerController pc = null;
        if (netObj != null)
            pc = netObj.GetComponent<PlayerController>();
        else
            pc = FindObjectsOfType<PlayerController>().FirstOrDefault(p => p.PlayerId == targetPlayerId);

        if (pc == null)
        {
            Debug.LogWarning($"HidePlayerClientRpc: player {targetPlayerId} not found on client.");
            return;
        }

        pc.SetModelVisibility(false);
    }
}