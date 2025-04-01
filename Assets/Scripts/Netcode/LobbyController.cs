using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

public class LobbyController : NetworkBehaviour
{
    NetworkVariable<Dictionary<ulong, bool>> playerReadiness = new NetworkVariable<Dictionary<ulong, bool>>(new Dictionary<ulong, bool>());

    public void ConnectPlayer(ulong playerId)
    {
        if (playerReadiness.Value.ContainsKey(playerId))
        {
            playerReadiness.Value[playerId] = false;
        } else
        {
            playerReadiness.Value.Add(playerId, false);
        }

        print($"Player connected: {playerId}, total count: {playerReadiness.Value.Count}");
    }
}
