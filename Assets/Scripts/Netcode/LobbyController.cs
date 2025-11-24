using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LobbyController : NetworkBehaviour
{
    private NetworkList<PlayerReadinessState> readinessList = new NetworkList<PlayerReadinessState>();
    private NetworkVariable<bool> hasStartedGame = new NetworkVariable<bool>(false);

    private void Start()
    {
        // Chat is now handled by ChatManager singleton
    }

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
    }

    public override void OnDestroy()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        // Use ChatManager singleton instead of finding chat controller
        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.SendPlayerJoinedServerRpc(clientId);
        }

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
        if (hasStartedGame.Value) return;
        ulong clientId = rpcParams.Receive.SenderClientId;

        for (int i = 0; i < readinessList.Count; i++)
        {
            if (readinessList[i].clientId == clientId)
            {
                bool newState = !readinessList[i].isReady;
                readinessList[i] = new PlayerReadinessState(clientId, newState);
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

        hasStartedGame.Value = true;
        MatchController matchController = GetComponent<MatchController>();
        matchController.StartGame(GetPlayerIdsList());
    }

    private List<ulong> GetPlayerIdsList()
    {
        List<ulong> ids = new List<ulong>();

        foreach (var player in readinessList)
        {
            ids.Add(player.clientId);
        }

        return ids;
    }
}