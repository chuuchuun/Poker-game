using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BotAI
{
    public enum HandStrength
    {
        Strong,
        Medium,
        Weak
    }

    private readonly float aggressiveness;
    private readonly float bluffChance;

    public BotAI(float aggressiveness = 0.6f, float bluffChance = 0.08f)
    {
        this.aggressiveness = Mathf.Clamp01(aggressiveness);
        this.bluffChance = Mathf.Clamp01(bluffChance);
    }

    public (BetAction action, int raiseAmount) DecideAction(IPlayerController player, RoundModel roundModel)
    {
        if (player == null || roundModel == null)
            return (BetAction.check, 0);

        int highest = roundModel.GetCurrentHighestBet();
        int requiredToCall = Math.Max(0, highest - player.CurrentBet);
        var tableCards = GameManager.Instance.GetComponent<DeckControlBehavior>().GetCardsOnTable() ?? new List<CardModel>();

        float effectiveAgg = aggressiveness;
        float effectiveBluff = bluffChance;

        try
        {
            var betting = GameObject.FindObjectOfType<BettingControlBehavior>();
            if (betting != null)
            {
                var activeIds = betting.GetActivePlayers() ?? new List<ulong>();
                var opponentIds = activeIds.Where(id => id != player.PlayerId).ToList();
                if (opponentIds.Count > 0)
                {
                    float maxOpp = 0f;
                    float avgOpp = 0f;
                    foreach (var id in opponentIds)
                    {
                        try
                        {
                            avgOpp += betting.GetPlayerBalance(id);
                            maxOpp = Mathf.Max(maxOpp, betting.GetPlayerBalance(id));
                        }
                        catch { }
                    }
                    avgOpp /= opponentIds.Count;

                    float balanceRatio = 1f;
                    if (maxOpp > 0f)
                        balanceRatio = (float)player.CurrentBalance / maxOpp;

                    float ratioMultiplier = Mathf.Clamp(Mathf.Lerp(0.6f, 1.25f, balanceRatio), 0.45f, 1.5f);

                    float callPressure = maxOpp > 0 ? (float)requiredToCall / Mathf.Max(1f, player.CurrentBalance) : 0f;
                    float pressurePenalty = Mathf.Clamp01(callPressure) * 0.6f;

                    effectiveAgg = Mathf.Clamp01(aggressiveness * ratioMultiplier * (1f - pressurePenalty));
                    effectiveBluff = Mathf.Clamp01(bluffChance * Mathf.Lerp(0.6f, 1.15f, balanceRatio));
                }
            }
        }
        catch
        {
            effectiveAgg = aggressiveness;
            effectiveBluff = bluffChance;
        }

        HandStrength strength = EvaluateHandStrength(player.CardsInHand, tableCards);

        float rnd = UnityEngine.Random.value;

        int ClampRaise(int desired)
        {
            desired = Mathf.Clamp(desired, requiredToCall, player.CurrentBalance);
            return Math.Max(desired, requiredToCall);
        }

        if (strength == HandStrength.Strong)
        {
            float raiseProb = 0.6f + (0.4f * effectiveAgg);
            if (rnd < raiseProb && player.CurrentBalance > requiredToCall)
            {
                int extra = Math.Max((int)(player.CurrentBalance * (0.05f + 0.15f * effectiveAgg)), requiredToCall);
                int raiseAmount = ClampRaise(requiredToCall + extra);
                return (BetAction.raise, raiseAmount);
            }

            if (requiredToCall > 0)
                return (BetAction.call, 0);

            return (BetAction.check, 0);
        }

        if (strength == HandStrength.Medium)
        {
            float callProb = 0.5f + 0.4f * effectiveAgg;
            if (requiredToCall == 0)
            {
                if (rnd < (0.15f * effectiveAgg))
                {
                    int raiseAmount = ClampRaise(Math.Max(1, (int)(player.CurrentBalance * 0.02f)));
                    return (BetAction.raise, raiseAmount);
                }
                return (BetAction.check, 0);
            }

            if (rnd < callProb && requiredToCall <= player.CurrentBalance)
            {
                if (rnd > 0.85f && player.CurrentBalance > requiredToCall + 5 && effectiveAgg > 0.7f)
                {
                    int raiseAmount = ClampRaise(requiredToCall + Math.Max(1, (int)(player.CurrentBalance * 0.03f)));
                    return (BetAction.raise, raiseAmount);
                }
                return (BetAction.call, 0);
            }

            if (requiredToCall > player.CurrentBalance * 0.25f)
                return (BetAction.fold, 0);

            if (requiredToCall <= player.CurrentBalance)
                return (BetAction.call, 0);

            return (BetAction.fold, 0);
        }

        {
            float effectiveBluffFinal = effectiveBluff * effectiveAgg;
            if (rnd < effectiveBluffFinal && player.CurrentBalance > requiredToCall)
            {
                int raiseAmount = ClampRaise(Math.Min(player.CurrentBalance, Math.Max(requiredToCall + 1, (int)(player.CurrentBalance * 0.05f))));
                return (BetAction.raise, raiseAmount);
            }

            if (requiredToCall == 0)
                return (BetAction.check, 0);

            if (requiredToCall <= player.CurrentBalance * 0.10f)
                return (BetAction.call, 0);

            return (BetAction.fold, 0);
        }
    }

    public HandStrength EvaluateHandStrength(List<CardModel> holeCards, List<CardModel> tableCards)
    {
        var known = new List<CardModel>();
        if (holeCards != null) known.AddRange(holeCards);
        if (tableCards != null) known.AddRange(tableCards);

        if (holeCards == null || holeCards.Count < 2)
            return HandStrength.Medium;

        var groups = known.GroupBy(c => c.value).Select(g => g.Count()).OrderByDescending(c => c).ToArray();
        if (groups.Length > 0 && groups[0] >= 3)
            return HandStrength.Strong;
        if (groups.Length > 0 && groups[0] == 2)
        {
            if (tableCards != null && tableCards.Count >= 3) return HandStrength.Strong;
            return HandStrength.Medium;
        }

        if (holeCards.Count >= 2 && holeCards[0].suit == holeCards[1].suit)
        {
            if (Math.Max(holeCards[0].value, holeCards[1].value) >= 11) return HandStrength.Medium;
        }

        double avg = known.Count > 0 ? known.Average(c => c.value) : 0f;
        if (avg >= 11f) return HandStrength.Medium;

        return HandStrength.Weak;
    }
}