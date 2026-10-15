using System.Collections.Generic;

// 낸 패를 "족보 문양 숫자" 문구로 만듦 (예: 스트레이트 해 1)
// 말풍선(ActionBubbleView)과 가운데 패 문구(LexioUI)가 같은 형식을 쓰도록 한곳에 둠
public class CombinationText
{
    // 기준 타일은 서열상 가장 높은 타일
    // 스트레이트의 맨 위 타일(1-2-3-4-5면 2, ~13-1이면 1)도 서열상 가장 높은 타일과 같음
    // 풀하우스/포카드는 문양을 비교하지 않으므로 트리플/포카드 숫자만 표시
    public string GetPlayText(Combination combination, int maxNumber)
    {
        string text;
        if (combination.Type == CombinationType.FullHouse)
        {
            text = combination.DisplayName + " " + GetNumberWithCount(combination.Tiles, 3);
        }
        else if (combination.Type == CombinationType.FourCard)
        {
            text = combination.DisplayName + " " + GetNumberWithCount(combination.Tiles, 4);
        }
        else
        {
            Tile top = GetHighestTile(combination.Tiles, maxNumber);
            text = combination.DisplayName + " " + GetSuitName(top.Suit) + " " + top.Number;
        }
        return text;
    }

    private Tile GetHighestTile(IReadOnlyList<Tile> tiles, int maxNumber)
    {
        Tile highest = tiles[0];
        for (int i = 1; i < tiles.Count; i++)
        {
            if (tiles[i].GetPower(maxNumber) > highest.GetPower(maxNumber))
            {
                highest = tiles[i];
            }
        }
        return highest;
    }

    // 정확히 count장 있는 숫자 (풀하우스의 트리플, 포카드의 4장)
    private int GetNumberWithCount(IReadOnlyList<Tile> tiles, int count)
    {
        int result = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            int sameCount = 0;
            for (int j = 0; j < tiles.Count; j++)
            {
                if (tiles[j].Number == tiles[i].Number)
                {
                    sameCount++;
                }
            }

            if (sameCount == count)
            {
                result = tiles[i].Number;
            }
        }
        return result;
    }

    private string GetSuitName(TileSuit suit)
    {
        string name;
        switch (suit)
        {
            case TileSuit.Cloud:
                name = "구름";
                break;
            case TileSuit.Star:
                name = "별";
                break;
            case TileSuit.Moon:
                name = "달";
                break;
            case TileSuit.Sun:
                name = "해";
                break;
            default:
                name = "";
                break;
        }
        return name;
    }
}
