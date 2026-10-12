using System.Collections.Generic;

public static class ScoreCalculator
{
    // 2를 n장 들고 있으면 남은 장수 × (n + 1)
    public static int GetEffectiveCount(IReadOnlyList<Tile> hand)
    {
        int twoCount = 0;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i].Number == 2)
            {
                twoCount++;
            }
        }
        return hand.Count * (twoCount + 1);
    }

    // 모든 쌍에 대해 유효 장수가 많은 쪽이 차이만큼 적은 쪽에게 지급
    public static int[] CalculateChipChanges(IReadOnlyList<int> effectiveCounts)
    {
        int[] changes = new int[effectiveCounts.Count];
        for (int i = 0; i < effectiveCounts.Count; i++)
        {
            for (int j = i + 1; j < effectiveCounts.Count; j++)
            {
                int difference = effectiveCounts[j] - effectiveCounts[i];
                changes[i] += difference;
                changes[j] -= difference;
            }
        }
        return changes;
    }

    // 칩 많은 순 순위, 동점은 공동 순위 (1, 1, 3 ...)
    public static int[] CalculateRanks(IReadOnlyList<int> chips)
    {
        int[] ranks = new int[chips.Count];
        for (int i = 0; i < chips.Count; i++)
        {
            int higherCount = 0;
            for (int j = 0; j < chips.Count; j++)
            {
                if (chips[j] > chips[i])
                {
                    higherCount++;
                }
            }
            ranks[i] = higherCount + 1;
        }
        return ranks;
    }
}
