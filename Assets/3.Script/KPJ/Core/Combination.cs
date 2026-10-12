using System.Collections.Generic;

// 5장 족보는 enum 순서가 곧 서열 (Straight < Flush < FullHouse < FourCard < StraightFlush)
public enum CombinationType
{
    None = 0,
    Single = 1,
    Pair = 2,
    Triple = 3,
    Straight = 4,
    Flush = 5,
    FullHouse = 6,
    FourCard = 7,
    StraightFlush = 8
}

public sealed class Combination
{
    public CombinationType Type { get; }
    public IReadOnlyList<Tile> Tiles { get; }

    // 같은 족보끼리 비교할 때 쓰는 값 (클수록 강함)
    public int Strength { get; }

    public int Count
    {
        get { return Tiles.Count; }
    }

    public Combination(CombinationType type, IReadOnlyList<Tile> tiles, int strength)
    {
        Type = type;
        Tiles = new List<Tile>(tiles);
        Strength = strength;
    }

    public string DisplayName
    {
        get { return GetDisplayName(); }
    }

    private string GetDisplayName()
    {
        string name;
        switch (Type)
        {
            case CombinationType.Single:
                name = "싱글";
                break;
            case CombinationType.Pair:
                name = "페어";
                break;
            case CombinationType.Triple:
                name = "트리플";
                break;
            case CombinationType.Straight:
                name = "스트레이트";
                break;
            case CombinationType.Flush:
                name = "플러시";
                break;
            case CombinationType.FullHouse:
                name = "풀하우스";
                break;
            case CombinationType.FourCard:
                name = "포카드";
                break;
            case CombinationType.StraightFlush:
                name = "스트레이트 플러시";
                break;
            default:
                name = "";
                break;
        }
        return name;
    }
}
