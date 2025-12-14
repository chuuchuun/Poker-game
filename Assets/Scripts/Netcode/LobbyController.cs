using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class LobbyController : NetworkBehaviour
{
    private NetworkList<PlayerReadinessState> readinessList = new NetworkList<PlayerReadinessState>();
    private NetworkVariable<bool> hasStartedGame = new NetworkVariable<bool>(false);

    MatchController matchController => GetComponent<MatchController>();

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
        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.SendPlayerJoinedServerRpc(clientId);
        }

        readinessList.Add(new PlayerReadinessState(clientId, false));
    }

    public void RegisterBot(ulong botId, bool isReady = true)
    {
        if (!IsServer) return;

        foreach (var entry in readinessList)
        {
            if (entry.clientId == botId) return;
        }

        readinessList.Add(new PlayerReadinessState(botId, isReady));
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