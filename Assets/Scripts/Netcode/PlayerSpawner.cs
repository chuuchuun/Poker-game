using Unity.Netcode;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.OnServerStarted += SpawnPlayer;
        }
    }

    private void SpawnPlayer()
    {
        var playerPrefab = Resources.Load("PlayerPrefab") as GameObject;
        NetworkObject playerObject = Instantiate(playerPrefab).GetComponent<NetworkObject>();
        playerObject.Spawn();
    }
}
