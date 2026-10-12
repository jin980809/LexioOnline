using System.Collections.Generic;

public static class CombinationEvaluator
{
    // 유효한 족보면 Combination, 아니면 null
    public static Combination Evaluate(IReadOnlyList<Tile> tiles, int maxNumber)
    {
        Combination result = null;
        if (tiles != null && tiles.Count > 0 && IsValidTileSet(tiles, maxNumber))
        {
            int count = tiles.Count;
            if (count == 1)
            {
                result = new Combination(CombinationType.Single, tiles, tiles[0].GetPower(maxNumber));
            }
            else if (count == 2)
            {
                result = EvaluateSameNumber(tiles, CombinationType.Pair, maxNumber);
            }
            else if (count == 3)
            {
                result = EvaluateSameNumber(tiles, CombinationType.Triple, maxNumber);
            }
            else if (count == 5)
            {
                result = EvaluateFive(tiles, maxNumber);
            }
        }
        return result;
    }

    // previous가 null이면 리드 상황이라 유효한 족보면 무엇이든 낼 수 있음
    public static bool CanBeat(Combination previous, Combination next)
    {
        bool result = false;
        if (next != null)
        {
            if (previous == null)
            {
                result = true;
            }
            else if (next.Count == previous.Count)
            {
                if (next.Type == previous.Type)
                {
                    result = next.Strength > previous.Strength;
                }
                else if (next.Count == 5)
                {
                    result = next.Type > previous.Type;
                }
            }
        }
        return result;
    }

    private static bool IsValidTileSet(IReadOnlyList<Tile> tiles, int maxNumber)
    {
        bool isValid = true;
        HashSet<byte> ids = new HashSet<byte>();
        for (int i = 0; i < tiles.Count; i++)
        {
            Tile tile = tiles[i];
            if (tile.Number < 1 || tile.Number > maxNumber || ids.Add(tile.Id) == false)
            {
                isValid = false;
            }
        }
        return isValid;
    }

    // 페어/트리플: 높은 수 → 높은 수의 문양 (같은 숫자이므로 가장 높은 문양의 power)
    private static Combination EvaluateSameNumber(IReadOnlyList<Tile> tiles, CombinationType type, int maxNumber)
    {
        Combination result = null;
        if (AreAllSameNumber(tiles))
        {
            result = new Combination(type, tiles, GetHighestPower(tiles, maxNumber));
        }
        return result;
    }

    private static Combination EvaluateFive(IReadOnlyList<Tile> tiles, int maxNumber)
    {
        Combination result = null;
        bool isFlush = AreAllSameSuit(tiles);
        int straightStrength = GetStraightStrength(tiles, maxNumber);
        bool isStraight = straightStrength >= 0;

        Dictionary<int, int> numberCounts = new Dictionary<int, int>();
        for (int i = 0; i < tiles.Count; i++)
        {
            int number = tiles[i].Number;
            if (numberCounts.ContainsKey(number))
            {
                numberCounts[number] = numberCounts[number] + 1;
            }
            else
            {
                numberCounts[number] = 1;
            }
        }

        int fourNumber = -1;
        int threeNumber = -1;
        int pairNumber = -1;
        foreach (KeyValuePair<int, int> pair in numberCounts)
        {
            if (pair.Value == 4)
            {
                fourNumber = pair.Key;
            }
            else if (pair.Value == 3)
            {
                threeNumber = pair.Key;
            }
            else if (pair.Value == 2)
            {
                pairNumber = pair.Key;
            }
        }

        if (isStraight && isFlush)
        {
            result = new Combination(CombinationType.StraightFlush, tiles, straightStrength);
        }
        else if (fourNumber > 0)
        {
            Tile fourTile = new Tile(TileSuit.Cloud, fourNumber);
            result = new Combination(CombinationType.FourCard, tiles, fourTile.GetRank(maxNumber));
        }
        else if (threeNumber > 0 && pairNumber > 0)
        {
            Tile threeTile = new Tile(TileSuit.Cloud, threeNumber);
            result = new Combination(CombinationType.FullHouse, tiles, threeTile.GetRank(maxNumber));
        }
        else if (isFlush)
        {
            // 가장 큰 수 → 그 큰 수의 문양
            result = new Combination(CombinationType.Flush, tiles, GetHighestPower(tiles, maxNumber));
        }
        else if (isStraight)
        {
            result = new Combination(CombinationType.Straight, tiles, straightStrength);
        }
        return result;
    }

    // 스트레이트가 아니면 -1
    // 서열: 1-2-3-4-5 > 2-3-4-5-6 > (n-3 ~ n, 1) > 최고 숫자가 높은 순
    // 순서가 같으면 서열상 가장 높은 타일의 문양으로 비교
    // (n-2, n-1, n, 1, 2) 같은 랩어라운드는 불가
    private static int GetStraightStrength(IReadOnlyList<Tile> tiles, int maxNumber)
    {
        int strength = -1;
        if (tiles.Count == 5)
        {
            bool[] hasNumber = new bool[maxNumber + 1];
            int distinctCount = 0;
            for (int i = 0; i < tiles.Count; i++)
            {
                int number = tiles[i].Number;
                if (hasNumber[number] == false)
                {
                    hasNumber[number] = true;
                    distinctCount++;
                }
            }

            if (distinctCount == 5)
            {
                int order = -1;
                int topNumber = -1;

                for (int start = 1; start + 4 <= maxNumber; start++)
                {
                    if (HasRange(hasNumber, start, start + 4))
                    {
                        if (start == 1)
                        {
                            order = maxNumber - 4;
                            topNumber = 2;
                        }
                        else if (start == 2)
                        {
                            order = maxNumber - 5;
                            topNumber = 2;
                        }
                        else
                        {
                            order = start - 3;
                            topNumber = start + 4;
                        }
                    }
                }

                if (order < 0)
                {
                    if (hasNumber[1] && HasRange(hasNumber, maxNumber - 3, maxNumber))
                    {
                        order = maxNumber - 6;
                        topNumber = 1;
                    }
                }

                if (order >= 0)
                {
                    TileSuit topSuit = TileSuit.Cloud;
                    for (int i = 0; i < tiles.Count; i++)
                    {
                        if (tiles[i].Number == topNumber)
                        {
                            topSuit = tiles[i].Suit;
                        }
                    }
                    strength = order * 4 + (int)topSuit;
                }
            }
        }
        return strength;
    }

    private static bool HasRange(bool[] hasNumber, int start, int end)
    {
        bool result = true;
        for (int number = start; number <= end; number++)
        {
            if (hasNumber[number] == false)
            {
                result = false;
            }
        }
        return result;
    }

    private static bool AreAllSameNumber(IReadOnlyList<Tile> tiles)
    {
        bool result = true;
        for (int i = 1; i < tiles.Count; i++)
        {
            if (tiles[i].Number != tiles[0].Number)
            {
                result = false;
            }
        }
        return result;
    }

    private static bool AreAllSameSuit(IReadOnlyList<Tile> tiles)
    {
        bool result = true;
        for (int i = 1; i < tiles.Count; i++)
        {
            if (tiles[i].Suit != tiles[0].Suit)
            {
                result = false;
            }
        }
        return result;
    }

    private static int GetHighestPower(IReadOnlyList<Tile> tiles, int maxNumber)
    {
        int highest = -1;
        for (int i = 0; i < tiles.Count; i++)
        {
            int power = tiles[i].GetPower(maxNumber);
            if (power > highest)
            {
                highest = power;
            }
        }
        return highest;
    }
}
