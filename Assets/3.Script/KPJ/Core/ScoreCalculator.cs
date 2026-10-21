using System.Collections.Generic;

public static class ScoreCalculator
{
    // 2를 1장 들 때마다 2배 (n장이면 남은 장수 × 2^n)
    public static int GetEffectiveCount(IReadOnlyList<Tile> hand)
    {
        int multiplier = 1;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i].Number == 2)
            {
                multiplier *= 2;
            }
        }
        return hand.Count * multiplier;
    }

    // 모든 쌍에 대해 유효 장수가 많은 쪽이 차이만큼 적은 쪽에게 지급
    // isLeaver가 true인 자리(게임 도중 나간 자리)는 정산에서 빠지고 칩이 고정됨 (COMPLETE-LEAVE-ABORT, 사용자 결정)
    public static int[] CalculateChipChanges(IReadOnlyList<int> effectiveCounts, IReadOnlyList<bool> isLeaver = null)
    {
        int[] changes = new int[effectiveCounts.Count];
        for (int i = 0; i < effectiveCounts.Count; i++)
        {
            for (int j = i + 1; j < effectiveCounts.Count; j++)
            {
                if (isLeaver != null && (isLeaver[i] || isLeaver[j]))
                {
                    continue;
                }
                int difference = effectiveCounts[j] - effectiveCounts[i];
                changes[i] += difference;
                changes[j] -= difference;
            }
        }
        return changes;
    }

    // 칩 많은 순 순위, 동점은 공동 순위 (1, 1, 3 ...)
    // 이탈자(isLeaver)는 칩과 상관없이 남은 사람들 다음 순위로 모두 공동 꼴등
    // 예: 5인전에서 2명 이탈 → 남은 3명이 칩 순으로 1~3위, 이탈자 2명은 둘 다 4위
    public static int[] CalculateRanks(IReadOnlyList<int> chips, IReadOnlyList<bool> isLeaver)
    {
        int[] ranks = new int[chips.Count];
        int stayedCount = 0;
        for (int i = 0; i < chips.Count; i++)
        {
            if (isLeaver[i] == false)
            {
                stayedCount++;
            }
        }

        for (int i = 0; i < chips.Count; i++)
        {
            if (isLeaver[i])
            {
                ranks[i] = stayedCount + 1;
            }
            else
            {
                int higherCount = 0;
                for (int j = 0; j < chips.Count; j++)
                {
                    if (isLeaver[j] == false && chips[j] > chips[i])
                    {
                        higherCount++;
                    }
                }
                ranks[i] = higherCount + 1;
            }
        }
        return ranks;
    }
}
