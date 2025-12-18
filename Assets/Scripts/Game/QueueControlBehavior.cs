using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

class QueuePlayerState
{
    public IPlayerController player;
    public bool hasFolded;
}

public class QueueControlBehavior : NetworkBehaviour
{
    private IPlayerController firstPlayer;
    private IPlayerController waitingForTurnPlayer;
    private bool passedFirstPlayer = false;
    List<QueuePlayerState> queuedPlayers = new List<QueuePlayerState>();

    DeckControlBehavior deckControlBehavior => GameManager.Instance.GetComponent<DeckControlBehavior>();
    BettingControlBehavior bettingControlBehavior => GameManager.Instance.GetComponent<BettingControlBehavior>();
    RoundModel roundModel => GameManager.Instance.GetComponent<RoundModel>();

    public void SetFirstPlayerToMove(IPlayerController player)
    {
        firstPlayer = player;
        waitingForTurnPlayer = player;

        waitingForTurnPlayer?.NotifyTurn(true);
    }

    public void SetPlayers(List<IPlayerController> players)
    {
        queuedPlayers = players.Select(p =>
        {
            return new QueuePlayerState
            {
                player = p,
                hasFolded = false
            };
        }).ToList();
    }

    public bool ShouldAcceptActionFromPlayer(IPlayerController player)
    {
        if (player == null || waitingForTurnPlayer == null) return false;
        return player.PlayerId == waitingForTurnPlayer.PlayerId;
    }

    public bool OnlyOnePlayerRemains()
    {
        int activeCount = 0;
        foreach (var s in queuedPlayers)
        {
            bool folded = s.hasFolded;
            if (s.player != null)
            {
                try
                {
                    folded = folded || bettingControlBehavior.HasPlayerFolded(s.player.PlayerId);
                }
                catch {}
            }

            if (!folded) activeCount++;
            if (activeCount > 1) return false;
        }
        return activeCount == 1;
    }

    public void StartRound()
    {
        waitingForTurnPlayer = firstPlayer;
        if (queuedPlayers.Count == 0) return;

        int index = queuedPlayers.FindIndex(s => s.player != null && firstPlayer != null && s.player.PlayerId == firstPlayer.PlayerId);
        if (index == -1)
        {
            index = queuedPlayers.FindIndex(s => s.player != null);
            if (index == -1) return;
        }

        var sbState = queuedPlayers[(index + queuedPlayers.Count - 2) % queuedPlayers.Count];
        var bbState = queuedPlayers[(index + queuedPlayers.Count - 1) % queuedPlayers.Count];

        bettingControlBehavior.SetBlinds(sbState.player?.PlayerId ?? 0UL, bbState.player?.PlayerId ?? 0UL);

        waitingForTurnPlayer?.NotifyTurn(true);
    }

    private void EndRound()
    {
        if (queuedPlayers.Count == 0)
        {
            roundModel.EndRound();
            return;
        }

        int index = queuedPlayers.FindIndex(s => s.player == firstPlayer);
        if (index == -1)
            index = 0;

        firstPlayer = queuedPlayers[(index + 1) % queuedPlayers.Count].player;
        roundModel.EndRound();
    }

    public void SwitchTurnToNextPlayer()
    {
        if (OnlyOnePlayerRemains())
        {
            EndRound();
            return;
        }

        if (queuedPlayers.Count == 0) return;

        int index = queuedPlayers.FindIndex(state => state.player != null && state.player.PlayerId == waitingForTurnPlayer?.PlayerId);
        if (index == -1)
        {
            index = queuedPlayers.FindIndex(s => !IsPlayerFoldedState(s));
            if (index == -1) return;
        }

        int newIndex = (index + 1) % queuedPlayers.Count;
        while (IsPlayerFoldedState(queuedPlayers[newIndex]) && newIndex != index)
        {
            newIndex = (newIndex + 1) % queuedPlayers.Count;
        }

        waitingForTurnPlayer?.NotifyTurn(false);

        waitingForTurnPlayer = queuedPlayers[newIndex].player;

        waitingForTurnPlayer?.NotifyTurn(true);

        if (waitingForTurnPlayer != null && firstPlayer != null && waitingForTurnPlayer.PlayerId == firstPlayer.PlayerId)
        {
            passedFirstPlayer = true;
        }

        if (passedFirstPlayer && !bettingControlBehavior.HasRaises())
        {
            if (deckControlBehavior.CanAddCardsToTable())
            {
                deckControlBehavior.AddCardsToTableServerRpc();
            }
            else
            {
                EndRound();
            }

            passedFirstPlayer = false;
        }
    }

    private bool IsPlayerFoldedState(QueuePlayerState state)
    {
        if (state == null) return true;
        bool folded = state.hasFolded;
        if (state.player != null)
        {
            try
            {
                folded = folded || bettingControlBehavior.HasPlayerFolded(state.player.PlayerId);
            }
            catch {}
        }
        return folded;
    }

    public ulong GetCurrentPlayerId()
    {
        return waitingForTurnPlayer != null ? waitingForTurnPlayer.PlayerId : 0UL;
    }

    public void MarkPlayerFoldedById(ulong playerId)
    {
        var state = queuedPlayers.FirstOrDefault(s => s.player != null && s.player.PlayerId == playerId);
        if (state != null)
        {
            state.hasFolded = true;
        }
    }
}
