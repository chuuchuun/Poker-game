using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LobbyController : NetworkBehaviour
{
    private NetworkList<PlayerReadinessState> readinessList = new NetworkList<PlayerReadinessState>();

    private NetworkVariable<bool> hasStartedGame = new NetworkVariable<bool>(false);

    public bool HasStartedGame()
    {
        return hasStartedGame.Value;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
        }

        readinessList.OnListChanged += OnReadinessListChanged;
    }

    private void OnDestroy()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        }

        readinessList.OnListChanged -= OnReadinessListChanged;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        Debug.Log($"Client connected (lobby): {clientId}");

        readinessList.Add(new PlayerReadinessState(clientId, false));
    }

    public void ToggleReadiness()
    {
        if (!IsClient) return;

        RequestToggleReadinessServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestToggleReadinessServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        for (int i = 0; i < readinessList.Count; i++)
        {
            if (readinessList[i].clientId == clientId)
            {
                bool newState = !readinessList[i].isReady;
                readinessList[i] = new PlayerReadinessState(clientId, newState);
                Debug.Log($"Player {clientId} readiness set to {newState}");
                break;
            }
        }

        StartGameIfReady();
    }

    private void StartGameIfReady()
    {
        if (readinessList.Count < 2) return;

        foreach (var player in readinessList)
        {
            if (!player.isReady)
                return;
        }

        Debug.Log("All players ready. Starting game...");
        hasStartedGame.Value = true;
        NotifyGameStartClientRpc();
    }

    private void OnReadinessListChanged(NetworkListEvent<PlayerReadinessState> change)
    {
        Debug.Log($"Readiness list changed. Type: {change.Type}, Value: {change.Value.clientId} isReady: {change.Value.isReady}");
    }

    [ClientRpc]
    private void NotifyGameStartClientRpc()
    {
        Debug.Log("Game started!");
    }
}
