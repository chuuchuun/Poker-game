using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;

public class QueueControlBehavior : NetworkBehaviour
{
    private ulong firstPlayerId;
    private ulong waitingForTurnOfPlayerWithId;
    private bool passedFirstPlayer = false;
    List<ulong> queuedPlayers = new List<ulong>();

    DeckControlBehavior deckControlBehavior => GameManager.Instance.GetComponent<DeckControlBehavior>();
    BettingControlBehavior bettingControlBehavior => GameManager.Instance.GetComponent<BettingControlBehavior>();
    RoundModel roundModel => GameManager.Instance.GetComponent<RoundModel>();

    public void SetFirstPlayerToMove(ulong id)
    {
        firstPlayerId = id;
        waitingForTurnOfPlayerWithId = id;
    }

    public void SetPlayers(List<ulong> players)
    {
        queuedPlayers = players;
    }

    public bool ShouldAcceptActionFromPlayerWithId(ulong id)
    {
        return id == waitingForTurnOfPlayerWithId;
    }

    public bool OnlyOnePlayerRemains()
    {
        return queuedPlayers
            .Select(p => bettingControlBehavior.HasPlayerFolded(p))
            .Where(hasFolded => hasFolded)
            .Count() == 1;
    }

    public void StartRound()
    {
        waitingForTurnOfPlayerWithId = firstPlayerId;
        var index = queuedPlayers.FindIndex(id => id == firstPlayerId);
        var sbPlayer = queuedPlayers[(index + queuedPlayers.Count - 2) % queuedPlayers.Count];
        var bbPlayer = queuedPlayers[(index + queuedPlayers.Count - 1) % queuedPlayers.Count];

        bettingControlBehavior.SetBlinds(sbPlayer, bbPlayer);
    }

    private void EndRound()
    {
        int index = queuedPlayers.FindIndex(id => id == firstPlayerId);
        firstPlayerId = queuedPlayers[(index + 1) % queuedPlayers.Count];
        roundModel.EndRound();
    }

    public void SwitchTurnToNextPlayer()
    {
        if (OnlyOnePlayerRemains())
        {
            EndRound();
            return;
        }

        int index = queuedPlayers.FindIndex(id => id == waitingForTurnOfPlayerWithId);
        int newIndex = (index + 1) % queuedPlayers.Count;
        while (bettingControlBehavior.HasPlayerFolded(queuedPlayers[newIndex]) && newIndex != index)
        {
            newIndex = (newIndex + 1) % queuedPlayers.Count;
        }

        waitingForTurnOfPlayerWithId = queuedPlayers[newIndex];
        if (waitingForTurnOfPlayerWithId == firstPlayerId)
        {
            passedFirstPlayer = true;
        }

        if (passedFirstPlayer && !bettingControlBehavior.HasRaises())
        {
            if (deckControlBehavior.CanAddCardsToTable())
            {
                deckControlBehavior.AddCardsToTableServerRpc();
            } else {
                EndRound();
            }

            passedFirstPlayer = false;
        }
    }
    public ulong GetCurrentPlayerId()
    {
        return waitingForTurnOfPlayerWithId;
    }
}
