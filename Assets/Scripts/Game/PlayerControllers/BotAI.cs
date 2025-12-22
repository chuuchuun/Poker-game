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
        if (holeCards == null || holeCards.Count < 2) return HandStrength.Weak;

        var allCards = new List<CardModel>();
        allCards.AddRange(holeCards);
        if (tableCards != null) allCards.AddRange(tableCards);

        if (tableCards == null || tableCards.Count == 0) return EvaluatePreFlopHand(holeCards);

        var playerHand = new HandEvaluator.PlayerHand { cards = allCards };
        var handState = HandEvaluator.GetPlayerHandStateStatic(playerHand.cards);

        var boardStrength = EvaluateBoardStrength(tableCards);
        var handRank = handState.rank;

        if (handRank == HandEvaluator.HandRank.HighCard)
        {
            var hasStrongDraw = HasStrongDraw(holeCards, tableCards);
            var highCardValue = allCards.Max(c => c.value);
            var boardHigh = tableCards.Max(c => c.value);
            var holeHigh = holeCards.Max(c => c.value);

            if (hasStrongDraw && highCardValue >= 11) return HandStrength.Medium;
            if (holeHigh > boardHigh && holeHigh >= 13) return HandStrength.Medium;

            var potential = CalculateOuts(holeCards, tableCards);
            if (potential >= 8) return HandStrength.Medium;

            return HandStrength.Weak;
        }

        if (handRank == HandEvaluator.HandRank.Pair)
        {
            var pairValue = allCards.GroupBy(c => c.value).First(g => g.Count() == 2).Key;
            var boardHigh = tableCards.Max(c => c.value);
            var isOverpair = holeCards[0].value == holeCards[1].value && holeCards[0].value > boardHigh;
            var isTopPair = holeCards.Any(hc => hc.value == pairValue) && pairValue == boardHigh;

            if (isOverpair)
            {
                if (pairValue >= 10) return HandStrength.Strong;
                if (pairValue >= 8) return HandStrength.Medium;
                return HandStrength.Weak;
            }

            if (isTopPair)
            {
                var holeKicker = holeCards.Where(c => c.value != pairValue).Max(c => c.value);
                if (pairValue >= 10 && holeKicker >= 11) return HandStrength.Medium;
                if (pairValue >= 9 && holeKicker >= 9) return HandStrength.Medium;

                var opponentsCanHave = CanOpponentsHaveBetterPair(pairValue, tableCards, holeCards);
                return opponentsCanHave ? HandStrength.Weak : HandStrength.Medium;
            }

            return HandStrength.Weak;
        }

        if (handRank == HandEvaluator.HandRank.TwoPair)
        {
            var pairs = allCards.GroupBy(c => c.value)
                .Where(g => g.Count() == 2)
                .OrderByDescending(g => g.Key)
                .Take(2)
                .ToList();
            var highPair = pairs[0].Key;
            var lowPair = pairs[1].Key;

            var usesBothHole = holeCards.All(hc => hc.value == highPair || hc.value == lowPair);

            if (usesBothHole)
            {
                if (highPair >= 12 && lowPair >= 9) return HandStrength.Strong;
                if (highPair >= 10) return HandStrength.Medium;
                return HandStrength.Weak;
            }

            var vulnerable = IsTwoPairVulnerable(highPair, lowPair, tableCards);
            if (vulnerable) return HandStrength.Weak;

            if (highPair >= 11 && lowPair >= 9) return HandStrength.Medium;
            if (highPair >= 9 && lowPair >= 7) return HandStrength.Medium;

            return HandStrength.Weak;
        }

        if (handRank == HandEvaluator.HandRank.ThreeOfAKind)
        {
            var tripsValue = allCards.GroupBy(c => c.value).First(g => g.Count() == 3).Key;
            var isSet = holeCards.Count(c => c.value == tripsValue) >= 2 ||
                       (holeCards.Any(c => c.value == tripsValue) && tableCards.Any(c => c.value == tripsValue));

            if (isSet)
            {
                if (tripsValue >= 10) return HandStrength.Strong;
                if (tripsValue >= 7) return HandStrength.Medium;
                return HandStrength.Weak;
            }

            var kickers = allCards.Where(c => c.value != tripsValue)
                .OrderByDescending(c => c.value)
                .Take(2)
                .ToList();
            var topKicker = kickers[0].value;

            var vulnerable = CanOpponentsHaveFullHouse(tripsValue, tableCards);
            if (vulnerable) return HandStrength.Weak;

            if (tripsValue >= 10 && topKicker >= 12) return HandStrength.Medium;
            if (tripsValue >= 8 && topKicker >= 10) return HandStrength.Medium;

            return HandStrength.Weak;
        }

        if (handRank == HandEvaluator.HandRank.Straight)
        {
            var straightHigh = allCards.OrderByDescending(c => c.value).First().value;

            var vulnerable = IsStraightVulnerable(straightHigh, tableCards);
            if (vulnerable) return HandStrength.Weak;

            if (straightHigh >= 13) return HandStrength.Strong;
            if (straightHigh >= 10) return HandStrength.Medium;

            return HandStrength.Weak;
        }

        if (handRank == HandEvaluator.HandRank.Flush)
        {
            var flushSuit = allCards.GroupBy(c => c.suit).First(g => g.Count() >= 5).Key;
            var flushCards = allCards.Where(c => c.suit == flushSuit)
                .OrderByDescending(c => c.value)
                .Take(5)
                .ToList();
            var flushHigh = flushCards[0].value;

            var hasNutFlushCard = holeCards.Any(c => c.suit == flushSuit && c.value == 14);
            var hasSecondNut = holeCards.Any(c => c.suit == flushSuit && c.value == 13);

            if (flushHigh == 14) return HandStrength.Strong;

            var vulnerable = IsFlushVulnerable(flushHigh, flushSuit, tableCards);
            if (vulnerable) return HandStrength.Weak;

            if (flushHigh >= 12) return HandStrength.Medium;
            if (hasNutFlushCard || hasSecondNut) return HandStrength.Medium;

            return HandStrength.Weak;
        }

        if (handRank == HandEvaluator.HandRank.FullHouse)
        {
            var groups = allCards.GroupBy(c => c.value)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ThenByDescending(g => g.Value)
                .ToList();
            var tripsValue = groups[0].Value;
            var pairValue = groups[1].Value;

            var vulnerable = CanOpponentsHaveBetterFullHouse(tripsValue, pairValue, tableCards);
            if (vulnerable) return HandStrength.Medium;

            if (tripsValue >= 10 || (tripsValue >= 7 && pairValue >= 10)) return HandStrength.Strong;
            if (tripsValue >= 6) return HandStrength.Medium;

            return HandStrength.Weak;
        }

        if (handRank >= HandEvaluator.HandRank.FourOfAKind) return HandStrength.Strong;

        return HandStrength.Weak;
    }

    private HandStrength EvaluatePreFlopHand(List<CardModel> holeCards)
    {
        holeCards = holeCards.OrderByDescending(c => c.value).ToList();
        int high = holeCards[0].value;
        int low = holeCards[1].value;
        bool suited = holeCards[0].suit == holeCards[1].suit;

        if (high == low)
        {
            if (high >= 10) return HandStrength.Strong;
            if (high >= 6) return HandStrength.Medium;
            return HandStrength.Weak;
        }

        int gap = high - low - 1;

        if (suited)
        {
            if (high == 14 && low >= 11) return HandStrength.Strong;
            if (high == 13 && low == 12) return HandStrength.Strong;
            if (high == 14 && low >= 9) return HandStrength.Medium;
            if (high == 13 && low == 11) return HandStrength.Medium;
            if (high == 12 && low == 11) return HandStrength.Medium;
            if (gap == 0 && high >= 10) return HandStrength.Medium;
            if (gap == 1 && high >= 11) return HandStrength.Medium;
            return HandStrength.Weak;
        }

        if (high == 14 && low >= 12) return HandStrength.Strong;
        if (high == 13 && low == 12) return HandStrength.Medium;
        if (high == 14 && low >= 10) return HandStrength.Medium;
        if (high >= 12 && low >= 11 && gap <= 1) return HandStrength.Medium;
        if (gap == 0 && high >= 11) return HandStrength.Medium;

        return HandStrength.Weak;
    }

    private bool HasStrongDraw(List<CardModel> holeCards, List<CardModel> tableCards)
    {
        if (tableCards == null || tableCards.Count < 3) return false;

        var allCards = holeCards.Concat(tableCards).ToList();

        var suitCounts = allCards.GroupBy(c => c.suit).Select(g => g.Count());
        bool hasFlushDraw = suitCounts.Any(count => count == 4);

        var values = allCards.Select(c => c.value).Distinct().OrderBy(v => v).ToList();
        bool hasOpenEnded = false;
        for (int i = 0; i <= values.Count - 4; i++)
        {
            if (values[i + 3] - values[i] == 3) hasOpenEnded = true;
        }

        return hasFlushDraw || hasOpenEnded;
    }

    private int CalculateOuts(List<CardModel> holeCards, List<CardModel> tableCards)
    {
        int outs = 0;
        var allCards = holeCards.Concat(tableCards).ToList();

        var suitGroups = allCards.GroupBy(c => c.suit);
        foreach (var group in suitGroups)
        {
            if (group.Count() == 4) outs += 9;
            if (group.Count() == 3) outs += 10;
        }

        var values = allCards.Select(c => c.value).Distinct().OrderBy(v => v).ToList();
        for (int i = 0; i <= values.Count - 4; i++)
        {
            if (values[i + 3] - values[i] == 3) outs += 8;
            if (values[i + 3] - values[i] == 4) outs += 4;
        }

        return Math.Min(outs, 15);
    }

    private bool CanOpponentsHaveBetterPair(int pairValue, List<CardModel> tableCards, List<CardModel> holeCards)
    {
        var boardValues = tableCards.Select(c => c.value).ToList();
        var boardHigh = boardValues.Max();

        if (pairValue < boardHigh)
        {
            var higherCardsOnBoard = boardValues.Count(v => v > pairValue);
            if (higherCardsOnBoard >= 2) return true;
        }

        var possibleHigherPairs = new List<int>();
        if (pairValue < 14) possibleHigherPairs.Add(14);
        if (pairValue < 13) possibleHigherPairs.Add(13);
        if (pairValue < 12) possibleHigherPairs.Add(12);

        var deadCards = holeCards.Concat(tableCards).Select(c => c.value).ToList();
        foreach (var higherValue in possibleHigherPairs)
        {
            var deadCount = deadCards.Count(v => v == higherValue);
            if (deadCount < 4) return true;
        }

        return false;
    }

    private bool IsTwoPairVulnerable(int highPair, int lowPair, List<CardModel> tableCards)
    {
        var boardValues = tableCards.Select(c => c.value).ToList();

        if (boardValues.GroupBy(v => v).Any(g => g.Count() >= 3)) return true;

        var possibleTrips = boardValues.GroupBy(v => v).Where(g => g.Count() == 2).Select(g => g.Key);
        foreach (var tripsValue in possibleTrips)
        {
            if (tripsValue > highPair) return true;
        }

        if (highPair <= 9 && boardValues.Max() >= 10) return true;

        return false;
    }

    private bool CanOpponentsHaveFullHouse(int tripsValue, List<CardModel> tableCards)
    {
        var boardGroups = tableCards.GroupBy(c => c.value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToList();

        var hasBoardPair = boardGroups.Any(g => g.Count == 2);
        var hasHigherBoardCard = boardGroups.Any(g => g.Value > tripsValue);

        if (hasBoardPair && hasHigherBoardCard) return true;

        var possibleFullHouse = false;
        foreach (var group in boardGroups.Where(g => g.Count == 2))
        {
            var neededValue = group.Value;
            var deadCards = tableCards.Count(c => c.value == neededValue);
            if (deadCards < 3) possibleFullHouse = true;
        }

        return possibleFullHouse;
    }

    private bool IsStraightVulnerable(int straightHigh, List<CardModel> tableCards)
    {
        var boardValues = tableCards.Select(c => c.value).Distinct().OrderBy(v => v).ToList();

        if (straightHigh <= 10)
        {
            var higherCards = boardValues.Where(v => v > straightHigh).ToList();
            if (higherCards.Count >= 2) return true;

            if (straightHigh == 10 && boardValues.Contains(14) && boardValues.Contains(13)) return true;
            if (straightHigh == 9 && boardValues.Contains(14) && boardValues.Contains(12)) return true;
        }

        var boardSorted = boardValues.OrderBy(v => v).ToList();
        for (int i = 0; i <= boardSorted.Count - 5; i++)
        {
            if (boardSorted[i + 4] - boardSorted[i] == 4)
            {
                if (boardSorted[i + 4] > straightHigh) return true;
            }
        }

        return false;
    }

    private bool IsFlushVulnerable(int flushHigh, CardSuit flushSuit, List<CardModel> tableCards)
    {
        var flushCardsOnBoard = tableCards.Count(c => c.suit == flushSuit);

        if (flushCardsOnBoard >= 3)
        {
            var boardFlushCards = tableCards.Where(c => c.suit == flushSuit).OrderByDescending(c => c.value).ToList();
            var boardFlushHigh = boardFlushCards.Count > 0 ? boardFlushCards[0].value : 0;

            if (boardFlushHigh > flushHigh) return true;

            if (flushHigh <= 11 && boardFlushCards.Count >= 2)
            {
                var secondBoardHigh = boardFlushCards.Count > 1 ? boardFlushCards[1].value : 0;
                if (secondBoardHigh >= 10) return true;
            }
        }

        var possibleHigherFlush = false;
        var missingHighCards = new List<int>();
        for (int value = flushHigh + 1; value <= 14; value++) missingHighCards.Add(value);

        var deadCards = tableCards.Where(c => c.suit == flushSuit).Select(c => c.value).ToList();
        foreach (var highValue in missingHighCards)
        {
            var deadCount = deadCards.Count(v => v == highValue);
            if (deadCount < 4) possibleHigherFlush = true;
        }

        return possibleHigherFlush;
    }

    private bool CanOpponentsHaveBetterFullHouse(int tripsValue, int pairValue, List<CardModel> tableCards)
    {
        var boardValues = tableCards.Select(c => c.value).ToList();
        var boardGroups = tableCards.GroupBy(c => c.value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToList();

        var higherTrips = boardGroups.Where(g => g.Value > tripsValue && g.Count >= 2).Any();
        if (higherTrips) return true;

        var possibleHigherFullHouse = false;
        foreach (var group in boardGroups.Where(g => g.Count == 3))
        {
            if (group.Value > tripsValue) return true;
        }

        foreach (var group in boardGroups.Where(g => g.Count == 2))
        {
            if (group.Value > pairValue)
            {
                var neededForTrips = 3 - group.Count;
                var deadCards = boardValues.Count(v => v == group.Value);
                if (deadCards < neededForTrips) possibleHigherFullHouse = true;
            }
        }

        return possibleHigherFullHouse;
    }

    private int EvaluateBoardStrength(List<CardModel> tableCards)
    {
        if (tableCards == null || tableCards.Count == 0) return 0;

        int strength = 0;

        var values = tableCards.Select(c => c.value).OrderBy(v => v).ToList();
        var suits = tableCards.Select(c => c.suit).ToList();

        var pairCount = values.GroupBy(v => v).Count(g => g.Count() >= 2);
        strength += pairCount * 3;

        var hasThreeSameSuit = suits.GroupBy(s => s).Any(g => g.Count() >= 3);
        if (hasThreeSameSuit) strength += 4;

        var hasFourSameSuit = suits.GroupBy(s => s).Any(g => g.Count() >= 4);
        if (hasFourSameSuit) strength += 6;

        var consecutiveCount = 0;
        for (int i = 0; i < values.Count - 1; i++)
        {
            if (values[i + 1] - values[i] == 1) consecutiveCount++;
        }
        strength += consecutiveCount * 2;

        var highCards = values.Count(v => v >= 11);
        strength += highCards;

        return strength;
    }

    private HandEvaluator.PlayerHandState GetPlayerHandState(HandEvaluator evaluator, List<CardModel> cards)
    {
        var method = typeof(HandEvaluator).GetMethod("GetPlayerHandState",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        return (HandEvaluator.PlayerHandState)method.Invoke(evaluator, new object[] { cards });
    }
}