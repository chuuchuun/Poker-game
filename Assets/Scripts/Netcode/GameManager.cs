using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
            int availableIndex = playerSpawnIndices.Count;
            if (availableIndex < spawnPoints.Length)
            {
                playerSpawnIndices[clientId] = availableIndex;
                SpawnPlayer(clientId, availableIndex);
            }
            else
            {
                Debug.LogWarning("Not enough spawn points!");
            }
        }
    }

    private void SpawnPlayer(ulong clientId, int spawnIndex)
    {
        Transform spawnPoint = spawnPoints[spawnIndex];
        GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        Debug.Log($"Spawning player {clientId} at {spawnPoint.position}");
    }

    private void OnEnable()
    {
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDisable()
    {
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
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
}
