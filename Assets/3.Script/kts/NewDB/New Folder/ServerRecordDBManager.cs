using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using UnityEngine;

public class GameResultInput
{
    public string gameId;
    public string userID;
    public int playerCount;  // 3, 4, 5
    public int rankPosition;
    public int coinChange;
}

public class ServerRecordDBManager : MonoBehaviour
{
    public static ServerRecordDBManager Instance { get; private set; }

    // 기존 서버 DB 매니저의 연결 문자열을 사용하도록 설정
    [SerializeField] private string connectionString;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private MySqlConnection GetConnection()
    {
        return new MySqlConnection(connectionString);
    }

    private string GetTableName(int playerCount)
    {
        switch (playerCount)
        {
            case 3: return "GameRecord3";
            case 4: return "GameRecord4";
            case 5: return "GameRecord5";
            default:
                throw new ArgumentOutOfRangeException(
           nameof(playerCount), "인원은 3, 4, 5만 가능합니다.");
        }
    }

    // 게임 서버가 판정한 한 판의 모든 참가자 결과를 전달
    public bool SaveGameResults(List<GameResultInput> results)
    {
        if (results == null || results.Count == 0 || results[0] == null)
            return false;

        int playerCount = results[0].playerCount;

        if (playerCount < 3 || playerCount > 5 ||
            results.Count != playerCount)
        {
            Debug.LogError("[RecordDB] 참가자 수가 올바르지 않습니다.");
            return false;
        }

        string gameId = results[0].gameId;

        if (string.IsNullOrWhiteSpace(gameId))
            return false;

        // 같은 게임의 결과인지, 중복 유저가 없는지 확인
        var users = new HashSet<string>();

        foreach (var r in results)
        {
            if (r == null ||
                r.playerCount != playerCount ||
                r.gameId != gameId ||
                string.IsNullOrWhiteSpace(r.userID) ||
                r.rankPosition < 1 ||
                r.rankPosition > playerCount ||
                !users.Add(r.userID))
            {
                Debug.LogError("[RecordDB] 결과 데이터 검증 실패");
                return false;
            }
        }

        string table = GetTableName(playerCount);

        try
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        // 먼저 모든 참가자의 전적을 저장
                        foreach (var r in results)
                        {
                            string sql = $@"
INSERT INTO {table}
    (gameId, userID, rankPosition, coinChange, playedAt)
VALUES
    (@gameId, @userID, @rankPosition, @coinChange, NOW());";

                            using (var cmd = new MySqlCommand(sql, conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@gameId", r.gameId);
                                cmd.Parameters.AddWithValue("@userID", r.userID);
                                cmd.Parameters.AddWithValue("@rankPosition", r.rankPosition);
                                cmd.Parameters.AddWithValue("@coinChange", r.coinChange);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 유저별 누적 통계 갱신
                        foreach (var r in results)
                        {
                            UpdateStatistics(conn, tx, r);
                        }

                        // 유저별 최근 전적을 10개만 남김
                        foreach (var r in results)
                        {
                            DeleteOldRecords(conn, tx, table, r.userID);
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }

            Debug.Log($"[RecordDB] 게임 결과 저장 완료: {gameId}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordDB] 저장 실패: {e}");
            return false;
        }
    }

    private void UpdateStatistics(
        MySqlConnection conn,
        MySqlTransaction tx,
        GameResultInput r)
    {
        string sql = @"
INSERT INTO UserStatistics
    (userID, totalGames, firstCount, secondCount,
     thirdCount, fourthCount, fifthCount, totalCoins, bestCoin)
VALUES
    (@userID, 1,
     IF(@rankPosition = 1, 1, 0),
     IF(@rankPosition = 2, 1, 0),
     IF(@rankPosition = 3, 1, 0),
     IF(@rankPosition = 4, 1, 0),
     IF(@rankPosition = 5, 1, 0),
     @coinChange, @coinChange)
ON DUPLICATE KEY UPDATE
    totalGames = totalGames + 1,
    firstCount = firstCount + IF(@rankPosition = 1, 1, 0),
    secondCount = secondCount + IF(@rankPosition = 2, 1, 0),
    thirdCount = thirdCount + IF(@rankPosition = 3, 1, 0),
    fourthCount = fourthCount + IF(@rankPosition = 4, 1, 0),
    fifthCount = fifthCount + IF(@rankPosition = 5, 1, 0),
    totalCoins = totalCoins + @coinChange,
    bestCoin = GREATEST(bestCoin, @coinChange);";

        using (var cmd = new MySqlCommand(sql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@userID", r.userID);
            cmd.Parameters.AddWithValue("@rankPosition", r.rankPosition);
            cmd.Parameters.AddWithValue("@coinChange", r.coinChange);
            cmd.ExecuteNonQuery();
        }
    }

    private void DeleteOldRecords(
        MySqlConnection conn,
        MySqlTransaction tx,
        string table,
        string userID)
    {
        string sql = $@"
DELETE FROM {table}
WHERE userID = @userID
  AND recordID NOT IN (
      SELECT recordID FROM (
          SELECT recordID
          FROM {table}
          WHERE userID = @userID
          ORDER BY playedAt DESC, recordID DESC
          LIMIT 10
      ) AS recent
  );";

        using (var cmd = new MySqlCommand(sql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@userID", userID);
            cmd.ExecuteNonQuery();
        }
    }

    public List<GameRecordData> GetRecentRecords(
        string userID, int playerCount = 0)
    {
        var records = new List<GameRecordData>();

        if (string.IsNullOrWhiteSpace(userID))
            return records;

        // 0 = 전체, 3/4/5 = 해당 인원 게임만
        if (playerCount != 0 &&
            playerCount != 3 &&
            playerCount != 4 &&
            playerCount != 5)
            return records;

        string[] tables = playerCount == 0
            ? new[] { "GameRecord3", "GameRecord4", "GameRecord5" }
            : new[] { GetTableName(playerCount) };

        try
        {
            using (var conn = GetConnection())
            {
                conn.Open();

                foreach (string table in tables)
                {
                    string sql = $@"
SELECT recordID, gameId, userID,
       rankPosition, coinChange, playedAt
FROM {table}
WHERE userID = @userID
ORDER BY playedAt DESC, recordID DESC
LIMIT 10;";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@userID", userID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                records.Add(new GameRecordData
                                {
                                    recordID = reader.GetInt64("recordID"),
                                    gameId = reader.GetString("gameId"),
                                    userID = reader.GetString("userID"),
                                    rankPosition = reader.GetInt32("rankPosition"),
                                    coinChange = reader.GetInt32("coinChange"),
                                    playedAt = reader.GetDateTime("playedAt")
                                        .ToString("yyyy-MM-dd HH:mm:ss")
                                });
                            }
                        }
                    }
                }
            }

            // 전체 검색이면 세 테이블의 결과를 합쳐 최신순으로 정렬
            records.Sort((a, b) =>
            {
                int timeCompare = string.CompareOrdinal(b.playedAt, a.playedAt);
                return timeCompare != 0
                    ? timeCompare
                    : b.recordID.CompareTo(a.recordID);
            });

            if (records.Count > 10)
                records.RemoveRange(10, records.Count - 10);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordDB] 전적 검색 실패: {e}");
            throw;
        }

        return records;
    }
}
