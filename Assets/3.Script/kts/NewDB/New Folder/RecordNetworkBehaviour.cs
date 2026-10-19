using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class RecordNetworkBehaviour : NetworkBehaviour
{
    // 클라이언트 UI에서 구독할 검색 결과 이벤트
    public System.Action<bool, string, GameRecordData[]> OnRecordsReceived;

    // 다른 서버 게임 로직에서 호출하는 저장 메서드
    [Server]
    public bool SaveResultsOnServer(List<GameResultInput> results)
    {
        if (ServerRecordDBManager.Instance == null)
        {
            Debug.LogError("[RecordNetwork] DB 매니저가 없습니다.");
            return false;
        }

        return ServerRecordDBManager.Instance.SaveGameResults(results);
    }

    // playerCount: 0=전체, 3=3인, 4=4인, 5=5인
    [Command]
    public void CmdRequestRecentRecords(string targetUserID, int playerCount)
    {
        if (string.IsNullOrWhiteSpace(targetUserID))
        {
            TargetReceiveRecords(
                connectionToClient, false, "유저 ID가 없습니다.", "");
            return;
        }

        if (playerCount != 0 &&
            playerCount != 3 &&
            playerCount != 4 &&
            playerCount != 5)
        {
            TargetReceiveRecords(
                connectionToClient, false, "잘못된 검색 조건입니다.", "");
            return;
        }

        var db = ServerRecordDBManager.Instance;

        if (db == null)
        {
            TargetReceiveRecords(
                connectionToClient, false, "DB 서버가 준비되지 않았습니다.", "");
            return;
        }

        List<GameRecordData> records =
            db.GetRecentRecords(targetUserID, playerCount);

        var wrapper = new GameRecordList
        {
            records = records.ToArray()
        };

        string json = JsonUtility.ToJson(wrapper);

        TargetReceiveRecords(
            connectionToClient, true, "전적 조회 완료", json);
    }

    [TargetRpc]
    private void TargetReceiveRecords(
        NetworkConnectionToClient target,
        bool success,
        string message,
        string json)
    {
        GameRecordData[] records = new GameRecordData[0];

        if (success && !string.IsNullOrEmpty(json))
        {
            GameRecordList wrapper =
                JsonUtility.FromJson<GameRecordList>(json);

            if (wrapper != null && wrapper.records != null)
                records = wrapper.records;
        }

        Debug.Log(
            $"[RecordNetwork] 검색 결과 수신: " +
            $"success={success}, count={records.Length}"
        );

        OnRecordsReceived?.Invoke(success, message, records);
    }
}
