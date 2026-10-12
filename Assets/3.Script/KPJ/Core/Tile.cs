using System;

// 문양 서열: 구름 < 별 < 달 < 해
public enum TileSuit
{
    Cloud = 0,
    Star = 1,
    Moon = 2,
    Sun = 3
}

public readonly struct Tile : IEquatable<Tile>
{
    public TileSuit Suit { get; }
    public int Number { get; }

    public Tile(TileSuit suit, int number)
    {
        Suit = suit;
        Number = number;
    }

    // 네트워크로 받은 1바이트 ID에서 복원
    public Tile(byte id)
    {
        Suit = (TileSuit)(id / 16);
        Number = id % 16;
    }

    // 네트워크 전송용 1바이트 표현 (문양 * 16 + 숫자)
    public byte Id
    {
        get { return (byte)((int)Suit * 16 + Number); }
    }

    // 숫자 서열: 3 < 4 < ... < maxNumber < 1 < 2 (0부터 시작하는 순위)
    public int GetRank(int maxNumber)
    {
        int rank;
        if (Number == 1)
        {
            rank = maxNumber - 2;
        }
        else if (Number == 2)
        {
            rank = maxNumber - 1;
        }
        else
        {
            rank = Number - 3;
        }
        return rank;
    }

    // 숫자 → 문양 순으로 비교하는 단일 타일 세기
    public int GetPower(int maxNumber)
    {
        return GetRank(maxNumber) * 4 + (int)Suit;
    }

    public bool Equals(Tile other)
    {
        return Suit == other.Suit && Number == other.Number;
    }

    public override bool Equals(object obj)
    {
        bool result = false;
        if (obj is Tile other)
        {
            result = Equals(other);
        }
        return result;
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public override string ToString()
    {
        return Suit + " " + Number;
    }
}
