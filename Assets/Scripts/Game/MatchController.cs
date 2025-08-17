using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class MatchController : NetworkBehaviour
{
    public bool wasGameStarted { get; private set; }
    private List<ulong> players;
    private int startingPlayer = 0;

    private void Awake()
    {
        wasGameStarted = false;
    }

    public void StartGame(List<ulong> playerIds)
    {
        this.players = playerIds;
        wasGameStarted = true;
        StartRound();
    }

    public void StartRound()
    {
        if (IsServer)
        {
            var roundModel = FindObjectOfType<RoundModel>();
            if (roundModel != null)
            {
                var netObj = roundModel.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    netObj = roundModel.gameObject.AddComponent<NetworkObject>();
                }
                if (!netObj.IsSpawned)
                {
                    netObj.Spawn();
                    Debug.Log("Spawned existing RoundModel");
                }
                // Spawn the object
   
                roundModel.StartGame(players.ToArray(), players[startingPlayer]);

            }
        }
    }

    public void RoundEnded()
    {

    }
}
