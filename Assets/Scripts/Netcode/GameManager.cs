using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using System.Data;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public GameObject playerPrefab;
    public Transform[] spawnPoints;

    private Dictionary<ulong, int> playerSpawnIndices = new Dictionary<ulong, int>();

    private void Awake()
    {
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
        // Register callbacks manually
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    
        Debug.Log("Callbacks manually registered.");
    
    }

    override public void OnDestroy()
    {
        // Unregister callbacks when the object is destroyed
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        Debug.Log("Callbacks manually unregistered.");

    }

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        Debug.Log("Hosting the game...");
        StartCoroutine(DelayedSpawnHost());
    }

    private IEnumerator DelayedSpawnHost()
    {
        yield return new WaitForSeconds(0.5f);
        AssignSpawnPoint(NetworkManager.Singleton.LocalClientId);
    }

    public void JoinGame()
    {
        NetworkManager.Singleton.StartClient();
        Debug.Log("Joining the game...");
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
            else
            {
                Debug.LogWarning("Not enough spawn points!");
            }
        }
    }

    private void ResetSpawnPoint(ulong clientId)
    {
        if (playerSpawnIndices.ContainsKey(clientId))
        {
            playerSpawnIndices.Remove(clientId);
        }
        else
        {
            Debug.Log("The spawning point of disconnected player wasn't cleared!");
        }
    }

    private void SpawnPlayer(ulong clientId, int spawnIndex)
    {
        if (!IsServer) return; // Ensure only the server runs this

        Transform spawnPoint = spawnPoints[spawnIndex];
        GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

        player.GetComponent<PlayerController>().SetSpawnIndex(spawnIndex);

        Debug.Log($"Spawning player {clientId} at {spawnPoint.position}");
    }


    [ClientRpc]
    private void UpdateClientPositionClientRpc(ulong clientId, Vector3 position)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            GameObject player = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId).gameObject;
            player.transform.position = position;
            Debug.Log($"Client {clientId} position updated to {position}");
        }
    }

    private void ClearHand(ulong clientId)
    {
        NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
        PlayerController playerController = playerNetworkObject.GetComponent<PlayerController>();

        if (playerController != null)
        {
            GameManager.FindObjectsOfType<RoundModel>()[0].BackToDeckServerRpc(new NetworkObjectReference(playerNetworkObject));
        }
        else
        {
            Debug.Log("Cannot return cards to the deck!");
        }
    }

    private void OnServerStarted()
    {
        Debug.Log("Server Started...");
        AssignSpawnPoint(NetworkManager.Singleton.LocalClientId);
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected");
        AssignSpawnPoint(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected");
        ClearHand(clientId);
        ResetSpawnPoint(clientId);
    }
}
