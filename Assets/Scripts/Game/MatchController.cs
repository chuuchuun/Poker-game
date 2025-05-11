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
        RoundModel roundModel = gameObject.AddComponent<RoundModel>();
        roundModel.StartGame(players[startingPlayer]);
    }

    public void RoundEnded()
    {

    }
}
