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
    private LobbyController lobbyController;

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
        if (!IsServer) return; // Ensure only the server runs this

        Transform spawnPoint = spawnPoints[spawnIndex];
        GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        player.GetComponent<PlayerController>().SetSpawnIndex(spawnIndex);
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
    }

    private void OnServerStarted()
    {
        AssignSpawnPoint(NetworkManager.Singleton.LocalClientId);
    }

    private void OnClientConnected(ulong clientId)
    {
        AssignSpawnPoint(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        ClearHand(clientId);
        ResetSpawnPoint(clientId);
    }
}
