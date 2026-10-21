using System;
using System.Collections.Generic;

// 서버에서만 돌아가는 게임 진행 로직 (Unity/Mirror 비의존)
public sealed class LexioGame
{
    private readonly Random random;
    private readonly List<List<Tile>> hands = new List<List<Tile>>();
    private readonly int[] chips;
    // 게임 도중 나간 자리. 칩 고정·정산 제외, 빈 바닥에서도 패스 가능
    private readonly bool[] isLeaver;

    public int PlayerCount { get; }
    public int MaxNumber { get; }
    // 한 게임에 진행할 라운드 수
    public int RoundCount { get; }
    public int RoundNumber { get; private set; }
    public int CurrentSeat { get; private set; } = -1;
    public int LastPlaySeat { get; private set; } = -1;
    public Combination LastCombination { get; private set; }
    public bool IsLeadTurn { get; private set; }
    public bool IsRoundOver { get; private set; }
    public bool IsFinished { get; private set; }

    // 직전 행동으로 모두 패스해서 리드가 넘어갔는지 (가운데 패 치우기용)
    public bool WasTrickCleared { get; private set; }

    public int[] LastChipChanges { get; private set; }

    // 직전 라운드 정산 기준 장수 (2를 든 만큼 늘어난 남은 장수). 클라이언트 정산 연출용
    public int[] LastEffectiveCounts { get; private set; }

    public LexioGame(int playerCount, int roundCount, int startingChips, Random random)
    {
        PlayerCount = playerCount;
        RoundCount = roundCount;
        MaxNumber = GameRules.GetMaxNumber(playerCount);
        this.random = random;
        chips = new int[playerCount];
        isLeaver = new bool[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            chips[i] = startingChips;
            hands.Add(new List<Tile>());
        }
    }

    public IReadOnlyList<Tile> GetHand(int seat)
    {
        return hands[seat];
    }

    public int GetChips(int seat)
    {
        return chips[seat];
    }

    public void SetLeaver(int seat)
    {
        isLeaver[seat] = true;
    }

    public bool IsLeaver(int seat)
    {
        return isLeaver[seat];
    }

    public void StartRound()
    {
        RoundNumber++;
        IsRoundOver = false;
        LastCombination = null;
        LastPlaySeat = -1;
        WasTrickCleared = false;
        LastChipChanges = null;
        LastEffectiveCounts = null;

        List<Tile> deck = new List<Tile>();
        for (int suit = 0; suit < GameRules.SuitCount; suit++)
        {
            for (int number = 1; number <= MaxNumber; number++)
            {
                deck.Add(new Tile((TileSuit)suit, number));
            }
        }

        for (int i = deck.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            Tile temp = deck[i];
            deck[i] = deck[swapIndex];
            deck[swapIndex] = temp;
        }

        int tilesPerPlayer = GameRules.GetTilesPerPlayer(PlayerCount);
        Tile cloudThree = new Tile(TileSuit.Cloud, 3);
        for (int seat = 0; seat < PlayerCount; seat++)
        {
            hands[seat].Clear();
            hands[seat].AddRange(deck.GetRange(seat * tilesPerPlayer, tilesPerPlayer));
            if (hands[seat].Contains(cloudThree))
            {
                CurrentSeat = seat;
            }
        }

        IsLeadTurn = true;
    }

    public bool TryPlay(int seat, IReadOnlyList<Tile> tiles)
    {
        bool isPlayed = false;
        if (IsRoundOver == false && seat == CurrentSeat && HasTiles(seat, tiles))
        {
            Combination combination = CombinationEvaluator.Evaluate(tiles, MaxNumber);
            if (combination != null)
            {
                Combination previous = null;
                if (IsLeadTurn == false)
                {
                    previous = LastCombination;
                }

                if (CombinationEvaluator.CanBeat(previous, combination))
                {
                    for (int i = 0; i < tiles.Count; i++)
                    {
                        hands[seat].Remove(tiles[i]);
                    }

                    LastCombination = combination;
                    LastPlaySeat = seat;
                    IsLeadTurn = false;
                    WasTrickCleared = false;
                    isPlayed = true;

                    if (hands[seat].Count == 0)
                    {
                        EndRound();
                    }
                    else
                    {
                        AdvanceTurn();
                    }
                }
            }
        }
        return isPlayed;
    }

    public bool TryPass(int seat)
    {
        bool isPassed = false;
        // 나간 자리는 빈 바닥(리드)에서도 패스해서 다음 자리가 리드함 (멈춤 방지)
        if (IsRoundOver == false && seat == CurrentSeat && (IsLeadTurn == false || isLeaver[seat]))
        {
            WasTrickCleared = false;
            AdvanceTurn();
            isPassed = true;
        }
        return isPassed;
    }

    // 서열상 가장 약한 타일 (리드 차례 시간 초과 / AI 리드용)
    public Tile GetWeakestTile(int seat)
    {
        List<Tile> hand = hands[seat];
        Tile weakest = hand[0];
        for (int i = 1; i < hand.Count; i++)
        {
            if (hand[i].GetPower(MaxNumber) < weakest.GetPower(MaxNumber))
            {
                weakest = hand[i];
            }
        }
        return weakest;
    }

    private bool HasTiles(int seat, IReadOnlyList<Tile> tiles)
    {
        bool result = tiles != null && tiles.Count > 0;
        if (result)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                if (hands[seat].Contains(tiles[i]) == false)
                {
                    result = false;
                }
            }
        }
        return result;
    }

    // 다음 사람으로 넘김. 한 바퀴 돌아 마지막으로 낸 사람에게 오면 리드 시작
    private void AdvanceTurn()
    {
        CurrentSeat = (CurrentSeat + 1) % PlayerCount;
        if (CurrentSeat == LastPlaySeat)
        {
            IsLeadTurn = true;
            LastCombination = null;
            WasTrickCleared = true;
        }
    }

    private void EndRound()
    {
        int[] effectiveCounts = new int[PlayerCount];
        for (int seat = 0; seat < PlayerCount; seat++)
        {
            effectiveCounts[seat] = ScoreCalculator.GetEffectiveCount(hands[seat]);
        }

        LastEffectiveCounts = effectiveCounts;
        LastChipChanges = ScoreCalculator.CalculateChipChanges(effectiveCounts, isLeaver);
        for (int seat = 0; seat < PlayerCount; seat++)
        {
            chips[seat] += LastChipChanges[seat];
        }

        IsRoundOver = true;
        CurrentSeat = -1;
        if (RoundNumber >= RoundCount)
        {
            IsFinished = true;
        }
    }
}
