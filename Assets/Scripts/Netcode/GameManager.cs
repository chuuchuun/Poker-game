using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public GameObject playerPrefab;
    public GameObject botPrefab;
    public Transform[] spawnPoints;
    public GameObject chatPrefab;
    public bool IsSingleplayer = false;

    private Dictionary<ulong, int> playerSpawnIndices = new Dictionary<ulong, int>();
    private LobbyController lobbyController;
    private bool isChatSpawned = false;

    private bool botsSpawned = false;

    private void Awake()
    {
        lobbyController = GetComponent<LobbyController>();
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        if (IsServer)
        {
            StartCoroutine(DelayedChatSpawn());
        }
    }

    override public void OnDestroy()
    {
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        StartCoroutine(DelayedSpawnHost());
    }

    private IEnumerator DelayedSpawnHost()
    {
        yield return new WaitForSeconds(0.5f);
        ulong userID = NetworkManager.Singleton.LocalClientId;
        AssignSpawnPoint(userID);
    }

    public void JoinGame()
    {
        NetworkManager.Singleton.StartClient();
    }

    private int GetFirstAvailableSpawnIndex()
    {
        var used = new HashSet<int>(playerSpawnIndices.Values);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (!used.Contains(i))
                return i;
        }
        return -1;
    }

    private void AssignSpawnPoint(ulong clientId)
    {
        if (playerSpawnIndices.ContainsKey(clientId)) return;

        int availableIndex = GetFirstAvailableSpawnIndex();
        if (availableIndex == -1)
        {
            return;
        }

        playerSpawnIndices[clientId] = availableIndex;
        SpawnPlayer(clientId, availableIndex);
    }

    private void ResetSpawnPoint(ulong clientId)
    {
        if (playerSpawnIndices.ContainsKey(clientId))
        {
            playerSpawnIndices.Remove(clientId);
        }
    }

    private void SpawnPlayer(ulong clientId, int spawnIndex)
    {
        if (!IsServer) return;

        Transform spawnPoint = spawnPoints[spawnIndex];
        GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        var pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.SetSpawnIndex(spawnIndex);

        if (IsSingleplayer && IsServer && !botsSpawned && clientId == NetworkManager.Singleton.LocalClientId)
        {
            SpawnBotsAfterPlayer();
        }
    }

    private void SpawnBotsAfterPlayer()
    {
        if (!IsServer) return;
        if (botPrefab == null)
        {
            Debug.LogError("Bot prefab is not assigned in GameManager!");
            return;
        }

        int maxBots = 4;
        int spawnedBots = 0;

        for (int botIndex = 0; botIndex < maxBots; botIndex++)
        {
            int availableIndex = GetFirstAvailableSpawnIndex();

            if (availableIndex == -1)
            {
                Debug.LogWarning("No available spawn points left to place bot.");
                break;
            }

            SpawnBot(availableIndex, botIndex);
            spawnedBots++;
        }

        botsSpawned = spawnedBots > 0;
        Debug.Log($"Spawned {spawnedBots} bots (IsSingleplayer={IsSingleplayer})");
    }

    private void SpawnBot(int spawnIndex, int botIndex)
    {
        if (!IsServer) return;
        if (botPrefab == null)
        {
            Debug.LogError("Bot prefab is not assigned in GameManager!");
            return;
        }

        Transform spawnPoint = spawnPoints[spawnIndex];
        GameObject botGO = Instantiate(botPrefab, spawnPoint.position, spawnPoint.rotation);

        var netObj = botGO.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("Bot prefab does not have a NetworkObject component.");
            Destroy(botGO);
            return;
        }

        netObj.SpawnWithOwnership(NetworkManager.ServerClientId);

        ulong syntheticBotId = 1000000UL + (ulong)botIndex;
        playerSpawnIndices[syntheticBotId] = spawnIndex;

        var botController = botGO.GetComponent<BotController>();
        if (botController != null)
        {
            botController.SetSpawnIndex(spawnIndex);
            botController.playerId = syntheticBotId;
        }

        if (lobbyController == null)
        {
            lobbyController = GetComponent<LobbyController>();
        }
        lobbyController?.RegisterBot(syntheticBotId, true);

        Debug.Log($"Spawned bot #{botIndex} at spawn index {spawnIndex} (syntheticId={syntheticBotId})");
    }

    private void SpawnChatSystem()
    {
        if (isChatSpawned)
        {
            Debug.Log("Chat system already spawned, skipping...");
            return;
        }

        if (chatPrefab == null)
        {
            Debug.LogError("Chat prefab is null! Please assign chat prefab in GameManager inspector.");
            return;
        }

        if (!IsServer)
        {
            Debug.LogWarning("Trying to spawn chat system but not server, skipping...");
            return;
        }

        try
        {
            Debug.Log("Spawning single chat system...");
            GameObject chatInstance = Instantiate(chatPrefab);
            NetworkObject chatNetworkObject = chatInstance.GetComponent<NetworkObject>();

            if (chatNetworkObject == null)
            {
                Debug.LogError("Chat prefab doesn't have NetworkObject component!");
                Destroy(chatInstance);
                return;
            }

            chatNetworkObject.SpawnWithOwnership(NetworkManager.ServerClientId, true);
            isChatSpawned = true;

            Debug.Log($"Chat system spawned successfully! NetworkObjectId: {chatNetworkObject.NetworkObjectId}");

            ChatManager chatManager = chatInstance.GetComponent<ChatManager>();
            if (chatManager == null)
            {
                Debug.LogError("Chat prefab doesn't have ChatManager component!");
            }
            else
            {
                Debug.Log("ChatManager found and ready!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to spawn chat system: {e.Message}");
        }
    }

    private IEnumerator DelayedChatSpawn()
    {
        yield return new WaitForSeconds(1f);

        if (IsServer && !isChatSpawned)
        {
            Debug.Log("Delayed chat spawn attempt...");
            SpawnChatSystem();
        }
    }

    private void ClearHand(ulong clientId)
    {
        NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
        if (playerNetworkObject == null) return;

        PlayerController playerController = playerNetworkObject.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.ClearHand();
        }
    }

    private void OnServerStarted()
    {
        AssignSpawnPoint(NetworkManager.Singleton.LocalClientId);
    }

    private void OnClientConnected(ulong clientId)
    {
        AssignSpawnPoint(clientId);

        if (IsServer && ChatManager.Instance != null)
        {
            ChatManager.Instance.SendPlayerJoinedServerRpc(clientId);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        ClearHand(clientId);
        ResetSpawnPoint(clientId);

        if (IsServer && ChatManager.Instance != null)
        {
            ChatManager.Instance.SendPlayerLeftServerRpc(clientId);
        }
    }

    public void DisconnectClient(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"Disconnecting and despawning client {clientId}");

        ClearHand(clientId);
        ResetSpawnPoint(clientId);

        NetworkObject playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
        if (playerObj != null && playerObj.IsSpawned)
        {
            foreach (NetworkObject child in playerObj.GetComponentsInChildren<NetworkObject>())
            {
                if (child != playerObj && child.IsSpawned)
                {
                    child.Despawn(true);
                    Debug.Log($"Despawned child object: {child.name}");
                }
            }

            playerObj.Despawn(true);
            Debug.Log($"Player {clientId} and child objects despawned.");
        }
        else
        {
            Debug.LogWarning($"Player object for client {clientId} not found or already despawned");
        }

        NetworkManager.Singleton.DisconnectClient(clientId);
        Debug.Log($"Client {clientId} disconnected from server.");
    }
}