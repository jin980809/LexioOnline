public static class GameRules
{
    public const int MinPlayers = 3;
    public const int MaxPlayers = 5;
    public const int StartingChips = 50;
    public const int RoundCount = 5;
    public const int SuitCount = 4;

    // 인원별 사용 숫자 범위: 3인 1~9, 4인 1~13, 5인 1~15
    public static int GetMaxNumber(int playerCount)
    {
        int maxNumber;
        if (playerCount <= 3)
        {
            maxNumber = 9;
        }
        else if (playerCount == 4)
        {
            maxNumber = 13;
        }
        else
        {
            maxNumber = 15;
        }
        return maxNumber;
    }

    public static int GetTilesPerPlayer(int playerCount)
    {
        return GetMaxNumber(playerCount) * SuitCount / playerCount;
    }
}
