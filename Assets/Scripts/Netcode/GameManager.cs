using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public GameObject playerPrefab;
    public Transform[] spawnPoints;
    public GameObject chatPrefab;

    private Dictionary<ulong, int> playerSpawnIndices = new Dictionary<ulong, int>();
    private LobbyController lobbyController;
    private bool isChatSpawned = false;

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

    private void AssignSpawnPoint(ulong clientId)
    {
        if (!playerSpawnIndices.ContainsKey(clientId))
        {
            if (playerSpawnIndices.Count < spawnPoints.Length)
            {
                for (int availableIndex = 0; availableIndex < spawnPoints.Length; availableIndex++)
                {
                    if (!playerSpawnIndices.ContainsValue(availableIndex))
                    {
                        playerSpawnIndices[clientId] = availableIndex;
                        SpawnPlayer(clientId, availableIndex);
                        break;
                    }
                }
            }
        }
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
        player.GetComponent<PlayerController>().SetSpawnIndex(spawnIndex);
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

            // Spawn the chat system for all clients
            chatNetworkObject.SpawnWithOwnership(NetworkManager.ServerClientId, true);
            isChatSpawned = true;

            Debug.Log($"Chat system spawned successfully! NetworkObjectId: {chatNetworkObject.NetworkObjectId}");

            // Verify chat manager component
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

        // Notify chat about player joining
        if (IsServer && ChatManager.Instance != null)
        {
            ChatManager.Instance.SendPlayerJoinedServerRpc(clientId);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        ClearHand(clientId);
        ResetSpawnPoint(clientId);

        // Notify chat about player leaving
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