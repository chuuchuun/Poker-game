using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;

class QueuePlayerState
{
    public ulong id;
    public bool hasFolded;
}

public class QueueControlBehavior : NetworkBehaviour
{
    private ulong firstPlayerId;
    private ulong waitingForTurnOfPlayerWithId;
    List<QueuePlayerState> queuedPlayers = new List<QueuePlayerState>();

    DeckControlBehavior deckControlBehavior => GameManager.Instance.GetComponent<DeckControlBehavior>();
    RoundModel roundModel => GameManager.Instance.GetComponent<RoundModel>();

    public void SetFirstPlayerToMove(ulong id)
    {
        firstPlayerId = id;
        waitingForTurnOfPlayerWithId = id;
    }

    public void SetPlayers(List<ulong> players)
    {
        queuedPlayers = players.Select(playerId =>
        {
            QueuePlayerState state = new QueuePlayerState
            {
                id = playerId,
                hasFolded = false
            };

            return state;
        }).ToList<QueuePlayerState>();
    }

    public bool ShouldAcceptActionFromPlayerWithId(ulong id)
    {
        return id == waitingForTurnOfPlayerWithId;
    }

    public void SwitchTurnToNextPlayer()
    {
        int index = queuedPlayers.FindIndex(state => state.id == waitingForTurnOfPlayerWithId);
        int newIndex = (index + 1) % queuedPlayers.Count;
        while (queuedPlayers[newIndex].hasFolded && newIndex != index)
        {
            newIndex = (newIndex + 1) % queuedPlayers.Count;
        }

        waitingForTurnOfPlayerWithId = queuedPlayers[newIndex].id;

        if (waitingForTurnOfPlayerWithId == firstPlayerId)
        {
            if (deckControlBehavior.CanAddCardsToTable())
            {
                deckControlBehavior.AddCardsToTableServerRpc();
            }
            else
            {
                roundModel.EndRound();
            }
        }
    }
    public ulong GetCurrentPlayerId()
    {
        return waitingForTurnOfPlayerWithId;
    }
}
