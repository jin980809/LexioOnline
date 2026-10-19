using System;

[Serializable]
public class GameRecordData
{
    public long recordID;
    public string gameId;
    public string userID;
    public int rankPosition;
    public int coinChange;
    public string playedAt;
}

[Serializable]
public class GameRecordList
{
    public GameRecordData[] records;
}

[Serializable]
public class UserStatisticsData
{
    public string userID;
    public int totalGames;
    public int firstCount;
    public int secondCount;
    public int thirdCount;
    public int fourthCount;
    public int fifthCount;
    public long totalCoins;
    public int bestCoin;
}
