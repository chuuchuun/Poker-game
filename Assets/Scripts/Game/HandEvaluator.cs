using System.Collections.Generic;
using System.Linq;
using Unity.Networking.Transport.Error;

public class HandEvaluator
{
    public enum HandRank
    {
        None = 0,
        HighCard = 1,
        Pair = 2,
        TwoPair = 3,
        ThreeOfAKind = 4,
        Straight = 5,
        Flush = 6,
        FullHouse = 7,
        FourOfAKind = 8,
        StraightFlush = 9,
        RoyalFlush = 10
    }

    public struct PlayerHand
    {
        public ulong id;
        public List<CardModel> cards;
    }

    public struct PlayerHandState
    {
        public HandRank rank;
        public List<CardModel> cards;

        public PlayerHandState(HandRank rank, List<CardModel> cards)
        {
            this.rank = rank;
            this.cards = cards;
        }
    }

    private static HandEvaluator sharedInstance = new HandEvaluator();

    private HandEvaluator() { }

    public static List<ulong> GetWinners(List<(ulong, List<CardModel>)> hands, List<CardModel> table)
    {
        return GetWinnersByHands(
            hands.Select(
                hand => new PlayerHand { id = hand.Item1, cards = hand.Item2.Concat(table).ToList() }
            ).ToList()
        );
    }

    private static List<ulong> GetWinnersByHands(List<PlayerHand> hands)
    {
        List<(ulong, PlayerHandState)> winners = new List<(ulong, PlayerHandState)>();
        hands.ForEach(hand =>
        {
            PlayerHandState state = sharedInstance.GetPlayerHandState(hand.cards);

            if (winners.Count() == 0)
            {
                winners.Add((hand.id, state));
            } else
            {
                PlayerHandState previousWinner = winners[0].Item2;
                int result = sharedInstance.CompareHands(previousWinner, state);
                if (result == 1) winners.Clear();

                if (result != -1) winners.Add((hand.id, state));
            }
        });

        return winners.Select(winner => winner.Item1).ToList();
    }

    public static PlayerHandState GetPlayerHandStateStatic(List<CardModel> cards)
    {
        return sharedInstance.GetPlayerHandState(cards);
    }

    public PlayerHandState GetPlayerHandState(List<CardModel> cards)
    {
        if (cards.Count < 5) return new PlayerHandState(HandRank.None, null);

        if (cards.Count > 5)
        {
            PlayerHandState bestHandState = new PlayerHandState(HandRank.None, null);
            for (int i = 0; i < cards.Count; i++)
            {
                List<CardModel> newCards = cards
                        .Where((item, index) => index != i)
                        .ToList();

                PlayerHandState newPlayerHandState = GetPlayerHandState(newCards);
                if (CompareHands(bestHandState, newPlayerHandState) == 1)
                {
                    bestHandState = newPlayerHandState;
                }
            }

            return bestHandState;
        }

        return new PlayerHandState(
            GetHandRank(ref cards),
            cards
        );
    }

    private int CompareHands(PlayerHandState first, PlayerHandState second)
    {
        if (first.rank != second.rank)
        {
            return first.rank > second.rank ? -1 : 1;
        }

        for (int i = 0; i < 5; i++)
        {
            CardModel firstCard = first.cards[i];
            CardModel secondCard = second.cards[i];

            if (firstCard.value != secondCard.value)
            {
                return firstCard.value > secondCard.value ? -1 : 1;
            }
        }

        return 0;
    }

    private HandRank GetHandRank(ref List<CardModel> cards)
    {
        cards = cards.OrderByDescending(card => card.value).ToList();

        if (IsRoyalFlush(cards)) return HandRank.RoyalFlush;
        if (IsStraightFlush(ref cards)) return HandRank.StraightFlush;
        if (IsFourOfAKind(cards)) return HandRank.FourOfAKind;
        if (IsFullHouse(cards)) return HandRank.FullHouse;
        if (IsFlush(cards)) return HandRank.Flush;
        if (IsStraight(ref cards)) return HandRank.Straight;
        if (IsThreeOfAKind(cards)) return HandRank.ThreeOfAKind;
        if (IsTwoPair(cards)) return HandRank.TwoPair;
        if (IsPair(cards)) return HandRank.Pair;

        return HandRank.HighCard;
    }

    private bool IsRoyalFlush(List<CardModel> cards)
    {
        cards = cards.OrderByDescending(card => card.value).ToList();

        if (
            !cards
            .Select(card => card.value)
            .SequenceEqual(new List<int> { 14, 13, 12, 11, 10 })
        ) return false;

        if (
            cards
            .Select(card => card.suit)
            .Distinct()
            .Count() != 1
        ) return false;

        return true;
    }

    private bool IsStraightFlush(ref List<CardModel> cards)
    {
        return IsFlush(cards) && IsStraight(ref cards);
    }

    private bool IsWheelStraight(List<CardModel> cards)
    {
        var values = cards
            .Select(card => card.value == 14 ? 1 : card.value)
            .OrderByDescending(value => value)
            .ToList();

        return values.SequenceEqual(new List<int> { 5, 4, 3, 2, 1 });
    }

    private bool IsFourOfAKind(List<CardModel> cards)
    {
        return cards
            .GroupBy(card => card.value)
            .Any(group => group.Count() == 4);
    }

    private bool IsFullHouse(List<CardModel> cards)
    {
        return cards.GroupBy(card => card.value).Count() == 2 &&
            cards.GroupBy(card => card.value).Any(group => group.Count() == 3);
    }

    private bool IsFlush(List<CardModel> cards)
    {
        return cards.GroupBy(card => card.suit).Count() == 1;
    }

    private bool IsStraight(ref List<CardModel> cards)
    {
        if (IsWheelStraight(cards))
        {
            cards = cards
                .Select(card =>
                    card.value == 14 ?
                    new CardModel { value = 1, suit = card.suit } :
                    card
                )
                .OrderByDescending(card => card.value)
                .ToList();

            return true;
        }

        if (
            cards
            .Select((card, index) => card.value + index)
            .Distinct()
            .Count() != 1
        ) return false;

        return true;
    }

    private bool IsThreeOfAKind(List<CardModel> cards)
    {
        return cards.GroupBy(card => card.value).Any(group => group.Count() == 3);
    }

    private bool IsTwoPair(List<CardModel> cards)
    {
        return cards.GroupBy(card => card.value).Count() == 3 &&
            cards.GroupBy(card => card.value).Any(group => group.Count() == 2);
    }

    private bool IsPair(List<CardModel> cards)
    {
        return cards.GroupBy(card => card.value).Any(group => group.Count() == 2);
    }
}