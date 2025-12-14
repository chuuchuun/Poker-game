using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class BotController : NetworkBehaviour, IPlayerController
{
    public ulong PlayerId => playeridBacking;
    private ulong playeridBacking;

    // Exposed backing field kept for compatibility
    public ulong playerId
    {
        get => playeridBacking;
        set => playeridBacking = value;
    }

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

    public int CurrentBet
    {
        get => currentBet;
        set => currentBet = value;
    }
    public int currentBet = 0;

    public List<CardModel> CardsInHand => cardsInHand;
    public List<Transform> CardSlots => cardSlots;
    public List<CardModel> cardsInHand = new List<CardModel>();
    public List<Transform> cardSlots = new List<Transform>();

    public List<ChipModel> TotalChips => totalChips;
    public List<ChipModel> totalChips = new List<ChipModel>();

    private List<ChipModel> blackChips = new List<ChipModel>();
    private List<ChipModel> redChips = new List<ChipModel>();
    private List<ChipModel> greenChips = new List<ChipModel>();
    private List<ChipModel> blueChips = new List<ChipModel>();

    private Transform chipBank;
    private Transform chipBankBlack;
    private Transform chipBankRed;
    private Transform chipBankGreen;
    private Transform chipBankBlue;

    private int spawnIndex = -1;
    private int chipCounter = 0;

    public NetworkVariable<int> networkBalance = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private GameObject chipPrefabBlack;
    [SerializeField] private GameObject chipPrefabRed;
    [SerializeField] private GameObject chipPrefabGreen;
    [SerializeField] private GameObject chipPrefabBlue;
    [SerializeField] private GameObject balancePrefab;

    private bool isThinking = false;

    public override void OnNetworkSpawn()
    {
        if (playeridBacking == 0)
            playeridBacking = OwnerClientId;

        networkBalance.OnValueChanged += OnNetworkBalanceChanged;

        if (IsServer)
        {
            AssignChipsToBot();
        }
        else
        {
            Invoke(nameof(FindMyChips), 0.5f);
        }
    }

    private void OnNetworkBalanceChanged(int oldValue, int newValue)
    {
        if (!IsServer)
        {
            currentBalance = newValue;
            OnBalanceChanged?.Invoke(newValue);
        }
    }

    private void FindMyChips()
    {
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
    }

    private void AssignChipsToBot()
    {
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
            Transform chipsRoot = transform.Find("Chips");
            if (chipsRoot == null)
            {
                GameObject root = new GameObject("Chips");
                root.transform.SetParent(transform);
                chipsRoot = root.transform;
            }
            chipsParent = new GameObject(color.ToString()).transform;
            chipsParent.SetParent(chipsRoot);
            chipsParent.localPosition = Vector3.zero;
        }

        for (int i = 0; i < count; i++)
        {
            GameObject chipGO;
            if (prefab != null)
            {
                chipGO = Instantiate(prefab, chipsParent);
            }
            else
            {
                chipGO = new GameObject($"chip_{color}_{i}");
                chipGO.transform.SetParent(chipsParent);
                chipGO.transform.localPosition = Vector3.zero;
                chipGO.AddComponent<NetworkObject>();
                chipGO.AddComponent<ChipModel>();
            }
            chipGO.transform.localPosition = new Vector3(0, 0.005f * i, 0);

            var networkObject = chipGO.GetComponent<NetworkObject>();
            if (networkObject != null && IsServer)
            {
                networkObject.SpawnWithOwnership(OwnerClientId);
            }

            var chipModel = chipGO.GetComponent<ChipModel>();
            if (chipModel == null)
            {
                chipModel = chipGO.AddComponent<ChipModel>();
            }
            chipModel.value = value;
            chipModel.color = color;
            chipModel.ownerClientId.Value = OwnerClientId;

            if (IsServer)
            {
                chipModel.chipId = GenerateChipId();
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

        return availableActions;
    }

    public void Act(BetAction action, int newBet = 0)
    {
        RoundModel roundModel = FindObjectOfType<RoundModel>();
        if (roundModel == null) return;

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

    private void HandleAutoPlay()
    {
        var roundModel = FindObjectOfType<RoundModel>();
        if (roundModel == null) return;

        int highest = roundModel.GetCurrentHighestBet();
        int requiredToCall = Math.Max(0, highest - currentBet);

        if (requiredToCall == 0)
        {
            Act(BetAction.check);
        }
        else if (requiredToCall <= CurrentBalance)
        {
            Act(BetAction.call);
        }
        else
        {
            Act(BetAction.fold);
        }
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

    private ChipModel GetChipByValue(int value)
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
        if (chip == null) return;

        var bank = FindObjectsOfType<Transform>().FirstOrDefault(t => t.GetInstanceID() == bankInstanceId);
        if (bank == null) return;

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
            }
        }
    }

    [ClientRpc]
    public void UpdateBalanceClientRpc(int newBalance)
    {
        if (!IsServer)
        {
            currentBalance = newBalance;
            OnBalanceChanged?.Invoke(newBalance);
        }
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

    private void Awake()
    {
        List<Transform> children = gameObject.GetComponentsInChildren<Transform>().ToList();
        foreach (Transform t in children)
        {
            if (t.CompareTag("hand_slot"))
            {
                cardSlots.Add(t);
            }
        }
    }

    private void Start()
    {
        if (balancePrefab != null)
        {
            var inst = Instantiate(balancePrefab, transform);
            var controller = inst.GetComponent<BalanceCanvasController>();
            if (controller != null)
            {
                controller.Initialize(this);
            }
        }
    }

    public void OnYourTurn()
    {
        if (!IsServer) return;

        if (isThinking) return;
        isThinking = true;

        StartCoroutine(PerformActionWithDelay(1f));
    }

    private IEnumerator PerformActionWithDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        if (!IsServer)
        {
            isThinking = false;
            yield break;
        }

        try
        {
            HandleAutoPlay();
        }
        finally
        {
            isThinking = false;
        }
    }

    public void NotifyTurn(bool myTurn)
    {
        if (myTurn && IsServer)
        {
            Debug.Log($"[BotController] Bot {playerId} notified it's their turn.");
            OnYourTurn();
        }
    }
}