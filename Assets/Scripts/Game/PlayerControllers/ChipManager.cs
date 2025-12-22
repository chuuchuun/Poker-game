using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ChipManager : NetworkBehaviour
{
    // Public lists for external access
    public List<ChipModel> TotalChips { get; private set; } = new List<ChipModel>();
    public List<ChipModel> blackChips { get; private set; } = new List<ChipModel>();
    public List<ChipModel> redChips { get; private set; } = new List<ChipModel>();
    public List<ChipModel> greenChips { get; private set; } = new List<ChipModel>();
    public List<ChipModel> blueChips { get; private set; } = new List<ChipModel>();

    // Networked balance
    public NetworkVariable<int> networkBalance = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int CurrentBalance
    {
        get => networkBalance.Value;
        set
        {
            if (!IsServer)
            {
                // Clients should not set network value directly.
                return;
            }

            if (networkBalance.Value != value)
            {
                networkBalance.Value = value;
                UpdateBalanceClientRpc(value);
                OnBalanceChanged?.Invoke(value);
            }
        }
    }

    public event Action<int> OnBalanceChanged;

    // Prefabs assigned in inspector
    [SerializeField] private GameObject chipPrefabBlack;
    [SerializeField] private GameObject chipPrefabRed;
    [SerializeField] private GameObject chipPrefabGreen;
    [SerializeField] private GameObject chipPrefabBlue;

    // Internal bank references (initialized when needed)
    private Transform chipBank;
    private Transform chipBankBlack;
    private Transform chipBankRed;
    private Transform chipBankGreen;
    private Transform chipBankBlue;

    private int chipCounter = 0;

    private void Awake()
    {
        networkBalance.OnValueChanged += (oldV, newV) =>
        {
            // Notify local listeners
            OnBalanceChanged?.Invoke(newV);
        };
    }

    public void AssignInitialChips()
    {
        if (!IsServer) return;

        TotalChips.Clear();
        blackChips.Clear();
        redChips.Clear();
        greenChips.Clear();
        blueChips.Clear();

        CurrentBalance = 0;

        SpawnChips(ChipColor.black, 25, 4, chipPrefabBlack);
        SpawnChips(ChipColor.red, 10, 4, chipPrefabRed);
        SpawnChips(ChipColor.green, 5, 4, chipPrefabGreen);
        SpawnChips(ChipColor.blue, 1, 5, chipPrefabBlue);

        CurrentBalance = TotalChips.Sum(c => c.value);
    }

    private int GenerateChipId()
    {
        if (IsServer)
        {
            return (int)(OwnerClientId * 1000) + chipCounter++;
        }
        return -1;
    }

    public void SpawnChips(ChipColor color, int value, int count, GameObject prefab)
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
                // spawn with player ownership so client-side sees ownership if needed
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

            TotalChips.Add(chipModel);

            switch (color)
            {
                case ChipColor.black: blackChips.Add(chipModel); break;
                case ChipColor.red: redChips.Add(chipModel); break;
                case ChipColor.green: greenChips.Add(chipModel); break;
                case ChipColor.blue: blueChips.Add(chipModel); break;
            }
        }

        if (IsServer)
        {
            CurrentBalance = TotalChips.Sum(c => c.value);
        }
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

    private void UpdateChipsToMeetRequirements(List<int> chips)
    {
        int[] currentChips = new int[4];
        int[] requiredChips = new int[4];

        int[][] tradeRules = new int[][] {
            new int[] { 1, -2, -1, 0 },
            new int[] { 0, 1, -2, 0 },
            new int[] { 0, 0, 1, -5 }
        };

        currentChips[0] = TotalChips.Count(c => c.color == ChipColor.black);
        requiredChips[0] = chips.Count(c => c == 25);

        currentChips[1] = TotalChips.Count(c => c.color == ChipColor.red);
        requiredChips[1] = chips.Count(c => c == 10);

        currentChips[2] = TotalChips.Count(c => c.color == ChipColor.green);
        requiredChips[2] = chips.Count(c => c == 5);

        currentChips[3] = TotalChips.Count(c => c.color == ChipColor.blue);
        requiredChips[3] = chips.Count(c => c == 1);

        var currentSum = currentChips[0] * 25 + currentChips[1] * 10 + currentChips[2] * 5 + currentChips[3] * 1;
        var requiredSum = requiredChips[0] * 25 + requiredChips[1] * 10 + requiredChips[2] * 5 + requiredChips[3] * 1;

        if (requiredSum > currentSum)
        {
            Debug.LogError("Not enough chips to meet the requirements.");
            return;
        }

        int[] chipDiff = new int[4];
        for (int i = 0; i < 4; i++) chipDiff[i] = currentChips[i] - requiredChips[i];

        List<int> actionSequence = new List<int>();

        while (chipDiff.Any(c => c < 0))
        {
            int bestRule = -1;
            int bestSign = 0;
            int bestScore = int.MinValue;

            for (int r = 0; r < tradeRules.Length; r++)
            {
                foreach (int sign in new[] { 1, -1 })
                {
                    int[] temp = new int[4];
                    for (int i = 0; i < 4; i++)
                        temp[i] = chipDiff[i] + tradeRules[r][i] * sign;

                    int score = 0;
                    for (int i = 0; i < 4; i++)
                        if (temp[i] < 0) score += temp[i];

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRule = r;
                        bestSign = sign;
                    }
                }
            }

            actionSequence.Add((bestRule + 1) * bestSign);

            for (int i = 0; i < 4; i++)
                chipDiff[i] += tradeRules[bestRule][i] * bestSign;
        }

        foreach (var action in actionSequence)
        {
            bool up = action > 0;
            ChipColor chipColor = action switch
            {
                1 => ChipColor.black,
                2 => ChipColor.red,
                3 => ChipColor.green,
                _ => ChipColor.blue
            };

            if (up) TradeUp(chipColor);
            else TradeDown(chipColor);
        }
    }

    private bool TradeUp(ChipColor color)
    {
        Dictionary<ChipColor, ChipColor[]> tradeRules = new Dictionary<ChipColor, ChipColor[]>()
        {
            { ChipColor.black, new[] { ChipColor.red, ChipColor.red, ChipColor.green } },
            { ChipColor.red, new[] { ChipColor.green, ChipColor.green } },
            { ChipColor.green, new[] { ChipColor.blue, ChipColor.blue, ChipColor.blue, ChipColor.blue, ChipColor.blue } }
        };

        if (!tradeRules.TryGetValue(color, out var chipsToRemove))
            return false;

        foreach (var chipToRemove in chipsToRemove)
        {
            int currentCount = chipToRemove switch
            {
                ChipColor.black => blackChips.Count,
                ChipColor.red => redChips.Count,
                ChipColor.green => greenChips.Count,
                ChipColor.blue => blueChips.Count,
                _ => 0
            };

            if (currentCount == 0)
            {
                if (!TradeUp(chipToRemove)) return false;
            }

            RemoveChipInstance(chipToRemove);
        }

        AddChipInstance(color);

        return true;
    }

    private bool TradeDown(ChipColor color)
    {
        Dictionary<ChipColor, ChipColor[]> tradeRules = new Dictionary<ChipColor, ChipColor[]>()
        {
            { ChipColor.black, new[] { ChipColor.red, ChipColor.red, ChipColor.green } },
            { ChipColor.red, new[] { ChipColor.green, ChipColor.green } },
            { ChipColor.green, new[] { ChipColor.blue, ChipColor.blue, ChipColor.blue, ChipColor.blue, ChipColor.blue } }
        };

        if (!tradeRules.TryGetValue(color, out var chipsToRemove))
            return false;

        foreach (var chipToRemove in chipsToRemove)
        {
            AddChipInstance(chipToRemove);
        }

        RemoveChipInstance(color);

        return true;
    }

    private void AddChipInstance(ChipColor color)
    {
        int value;
        GameObject prefab;

        (value, prefab) = color switch {
            ChipColor.black => (25, chipPrefabBlack),
            ChipColor.red => (10, chipPrefabRed),
            ChipColor.green => (5, chipPrefabGreen),
            ChipColor.blue => (1, chipPrefabBlue)
        };

        SpawnChips(color, value, 1, prefab);
    }

    private void RemoveChipInstance(ChipColor color)
    {
        var chip = DropChip(color);
        if (chip == null) return;

        var netObj = chip.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }
        else
        {
            Destroy(chip.gameObject);
        }
    }

    public List<ChipModel> RemoveChip(int bet)
    {
        List<ChipModel> chipsToRemove = new List<ChipModel>();
        int[] requiredChips = RequiredChips(bet);
        UpdateChipsToMeetRequirements(requiredChips.ToList());

        foreach (int chipValue in requiredChips)
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
            }
            else
            {
                break;
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

    ChipModel DropChip(ChipColor color)
    {
        switch (color)
        {
            case ChipColor.black:
                var b = blackChips.FirstOrDefault();
                if (b != null)
                {
                    blackChips.Remove(b);
                    TotalChips.Remove(b);
                }
                return b;
            case ChipColor.red:
                var r = redChips.FirstOrDefault();
                if (r != null)
                {
                    redChips.Remove(r);
                    TotalChips.Remove(r);
                }
                return r;
            case ChipColor.green:
                var g = greenChips.FirstOrDefault();
                if (g != null)
                {
                    greenChips.Remove(g);
                    TotalChips.Remove(g);
                }
                return g;
            case ChipColor.blue:
                var bl = blueChips.FirstOrDefault();
                if (bl != null)
                {
                    blueChips.Remove(bl);
                    TotalChips.Remove(bl);
                }
                return bl;
            default:
                return null;
        }
    }

    ChipModel GetChipByValue(int value)
    {
        return value switch
        {
            25 => blackChips.FirstOrDefault(),
            10 => redChips.FirstOrDefault(),
            5 => greenChips.FirstOrDefault(),
            1 => blueChips.FirstOrDefault(),
            _ => null,
        };
    }

    [ServerRpc(RequireOwnership = false)]
    public void MoveChipsToBankServerRpc(ulong[] chipIds)
    {
        if (!IsServer) return;

        InitializeBank();

        var chipsToRemove = TotalChips
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

            // remove original player chip (despawn/destroy)
            var netObj = chip.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(chip.gameObject);
            }

            // remove from collections
            TotalChips.Remove(chip);
            switch (chip.color)
            {
                case ChipColor.black: blackChips.Remove(chip); break;
                case ChipColor.red: redChips.Remove(chip); break;
                case ChipColor.green: greenChips.Remove(chip); break;
                case ChipColor.blue: blueChips.Remove(chip); break;
            }

            // spawn a new chip under the bank (server-spawned, owner = server)
            SpawnBankChip(chip.color, chip.value, targetParent, stackPosition);
        }

        // update balance after moving chips to bank
        CurrentBalance = TotalChips.Sum(c => c.value);
    }

    private void SpawnBankChip(ChipColor color, int value, Transform parent, int stackPosition)
    {
        GameObject prefab = GetPrefabForValue(value);

        GameObject chipGO;
        if (prefab != null)
        {
            chipGO = Instantiate(prefab, parent);
        }
        else
        {
            chipGO = new GameObject($"bank_chip_{color}_{stackPosition}");
            chipGO.transform.SetParent(parent);
            chipGO.AddComponent<NetworkObject>();
            chipGO.AddComponent<ChipModel>();
        }

        chipGO.transform.localPosition = new Vector3(0, stackPosition * 0.005f, 0);
        chipGO.transform.localRotation = Quaternion.identity;

        var netObj = chipGO.GetComponent<NetworkObject>();
        if (netObj != null && IsServer)
        {
            // bank chips owned by server (no ownership assigned to client)
            netObj.Spawn();
        }

        var chipModel = chipGO.GetComponent<ChipModel>();
        if (chipModel == null)
            chipModel = chipGO.AddComponent<ChipModel>();

        chipModel.value = value;
        chipModel.color = color;
        chipModel.ownerClientId.Value = 0; // bank
        chipModel.stackPosition.Value = stackPosition;

        if (IsServer)
            chipModel.chipId = GenerateChipId();
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
            networkBalance.Value = newBalance;
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

        // Collect children first to avoid modifying collection during iteration
        var children = new List<Transform>();
        foreach (Transform child in colorBank)
            children.Add(child);

        foreach (Transform child in children)
        {
            if (child.TryGetComponent<ChipModel>(out var chip))
            {
                var netObj = child.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    Destroy(child.gameObject);
                }
            }
            else
            {
                // if not a ChipModel (just a transform), destroy it anyway
                Destroy(child.gameObject);
            }
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
        CurrentBalance = TotalChips.Sum(c => c.value);
    }

    public void AddChipsFromBank(List<ChipModel> chips)
    {
        foreach (var chip in chips)
        {
            // remove bank identity and give to player
            chip.ownerClientId.Value = OwnerClientId;

            // ensure internal collections reflect new ownership
            TotalChips.Add(chip);

            switch (chip.color)
            {
                case ChipColor.black: blackChips.Add(chip); break;
                case ChipColor.red: redChips.Add(chip); break;
                case ChipColor.green: greenChips.Add(chip); break;
                case ChipColor.blue: blueChips.Add(chip); break;
            }

            // move under owner transform
            Transform targetParent = transform.Find("Chips/" + chip.color.ToString());
            if (targetParent == null)
            {
                Transform chipsRoot = transform.Find("Chips");
                if (chipsRoot == null)
                {
                    GameObject root = new GameObject("Chips");
                    root.transform.SetParent(transform);
                    chipsRoot = root.transform;
                }
                targetParent = new GameObject(chip.color.ToString()).transform;
                targetParent.SetParent(chipsRoot);
                targetParent.localPosition = Vector3.zero;
            }

            // If chip has a NetworkObject and currently spawned by server as bank chip, reparenting alone is fine for visuals.
            // We leave network ownership as-is (chip.ownerClientId.Value changed above for game logic).
            chip.transform.SetParent(targetParent);
            chip.transform.localPosition = new Vector3(0, targetParent.childCount * 0.005f, 0);
            chip.transform.localRotation = Quaternion.identity;
        }

        CurrentBalance = TotalChips.Sum(c => c.value);
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

    public void InitializeChips()
    {
        // Categorize existing TotalChips into per-color lists
        blackChips.Clear();
        redChips.Clear();
        greenChips.Clear();
        blueChips.Clear();
        foreach (var chip in TotalChips)
        {
            switch (chip.color)
            {
                case ChipColor.black: blackChips.Add(chip); break;
                case ChipColor.red: redChips.Add(chip); break;
                case ChipColor.green: greenChips.Add(chip); break;
                case ChipColor.blue: blueChips.Add(chip); break;
            }
        }

        CurrentBalance = TotalChips.Sum(c => c.value);
    }

    // New method: replace player's entire chip set with a new distribution.
    // Destroys existing player-owned chip instances and spawns new ones owned by this player.
    public void UpdateChips(List<int> chipValues)
    {
        if (!IsServer) return;

        // destroy existing chips owned by player
        var existing = TotalChips.ToList();
        foreach (var chip in existing)
        {
            var netObj = chip.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(chip.gameObject);
            }
        }

        // clear tracking lists
        TotalChips.Clear();
        blackChips.Clear();
        redChips.Clear();
        greenChips.Clear();
        blueChips.Clear();

        // spawn new chips grouped by denomination
        var groups = chipValues.GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());
        foreach (var kvp in groups)
        {
            int value = kvp.Key;
            int count = kvp.Value;
            SpawnChips(GetColorForValue(value), value, count, GetPrefabForValue(value));
        }

        CurrentBalance = TotalChips.Sum(c => c.value);
    }
}
