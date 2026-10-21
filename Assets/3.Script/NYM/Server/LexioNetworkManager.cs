using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

/*
	Documentation: https://mirror-networking.gitbook.io/docs/components/network-manager
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkManager.html
*/

// KPJ LexioTableNetworkManager를 상속해 대기열·매치별 테이블을 붙인다(KPJ 코드 수정 없음).
// KPJ LexioPlayer·LexioTable은 NetworkManager.singleton as LexioTableNetworkManager로 찾으므로 그대로 동작한다.
// KPJ의 단일 테이블 생성(OnStartServer)·입장 차단(OnServerConnect)은 호출하지 않고, 이탈 처리(OnServerDisconnect)·클라 이벤트는 그대로 쓴다.
public class LexioNetworkManager : LexioTableNetworkManager
{
    // Overrides the base singleton so we don't
    // have to cast to this type everywhere.
    public static new LexioNetworkManager singleton => (LexioNetworkManager)NetworkManager.singleton;

    [Header("렉시오 서버 설정")]
    [Tooltip("이 서버에서 동시에 진행할 수 있는 최대 경기 수. 꽉 차면 정원이 모여도 빈 경기 슬롯이 생길 때까지 대기")]
    [SerializeField] int maxMatches = 20;

    [Tooltip("경기마다 하나씩 생성할 테이블 프리팹. NetworkMatch 컴포넌트 필요(Player Prefab도 동일)")]
    [SerializeField] LexioTable matchTablePrefab;

    [Header("렉시오 정산 복구")]
    [Tooltip("비우면 persistentDataPath/settlement-journal.json. 입장료 환불·완료 저장이 DB 오류·서버 재시작 뒤에도 한 번만 반영되도록 미완 정산을 보관")]
    [SerializeField] string settlementJournalPath = "";

    [Header("렉시오 테스트")]
    [Tooltip("0이 아니면 클라 접속 직후 이 인원(3/4/5) 대기열에 자동 신청. 매칭 UI 연결 전 테스트용")]
    [SerializeField] int autoJoinMatchSize;

    // ───────── 서버 전용 상태 (매칭·경기 소속) ─────────
    // 경기 진행(손패·점수 등)은 KPJ LexioTable이 맡는다. 여기는 연결 ↔ 대기열/경기 소속·테이블 수명만 관리.

    readonly Dictionary<int, List<NetworkConnectionToClient>> queues = new Dictionary<int, List<NetworkConnectionToClient>>
    {
        { 3, new List<NetworkConnectionToClient>() },
        { 4, new List<NetworkConnectionToClient>() },
        { 5, new List<NetworkConnectionToClient>() }
    };
    readonly Dictionary<NetworkConnectionToClient, int> queuedSize = new Dictionary<NetworkConnectionToClient, int>();
    readonly Dictionary<Guid, List<NetworkConnectionToClient>> matches = new Dictionary<Guid, List<NetworkConnectionToClient>>();
    readonly Dictionary<NetworkConnectionToClient, Guid> connectionMatch = new Dictionary<NetworkConnectionToClient, Guid>();
    readonly Dictionary<Guid, LexioTable> matchTables = new Dictionary<Guid, LexioTable>();
    // 정원이 모여 입장료 DB 차감을 기다리는 연결. 이 동안 취소·재신청을 막는다.
    readonly HashSet<NetworkConnectionToClient> starting = new HashSet<NetworkConnectionToClient>();
    int startingMatches;
    // 정산 저널(서버 로컬 JSON). DB 원장 matchsettlement가 같은 matchId 중복 반영을 막고, 저널은 해야 할 환불·완료 저장을 재시작 뒤에도 잃지 않게 보관한다.
    [Serializable]
    class Settlement
    {
        public string matchId;
        public int state;         // SettlementStarting·Started·Finished·Failed
        public int matchSize;
        public string[] userIds;  // Starting/Started: 시작 좌석 순 계정. Finished: 저장 대상(유효 참가자)
        public int[] images;      // 시작 좌석 순 색상 스냅샷(이탈자 포함)
        public int[] ranks, finalChips, seatIndexes;
        public long finishedAtUtcTicks;
    }

    [Serializable]
    class SettlementJournal { public List<Settlement> entries = new List<Settlement>(); }

    // Starting=차감 중/시작 전(남아 있으면 환불), Started=경기 중(중단 시 입장료 유지), Finished=완료 결과 확정(저장 재시도), Failed=영구 거절(운영 확인용 보관)
    const int SettlementStarting = 0, SettlementStarted = 1, SettlementFinished = 2, SettlementFailed = 3;
    readonly Dictionary<string, Settlement> settlements = new Dictionary<string, Settlement>();
    // DB 반영을 기다리는 정산(Starting=환불, Finished=완료 저장). 해당 계정은 재매칭을 막는다.
    readonly Dictionary<string, (float at, int attempts)> retries = new Dictionary<string, (float at, int attempts)>();
    readonly HashSet<string> settling = new HashSet<string>(); // DB 호출 중
    bool journalReadable = true; // 읽기 실패한 저널은 덮어쓰지 않는다(새 경기 시작도 막힘)

    int nextRequestId;

    /// <summary>[클라] 매칭 신청/취소 결과.</summary>
    public event Action<RequestResult> QueueResultReceived;

    /// <summary>[클라] 내가 기다리는 대기열 인원 변경.</summary>
    public event Action<QueueUpdated> QueueUpdatedReceived;

    /// <summary>로그인한 연결의 계정 키. AccountAuthenticator가 인증 성공 시 conn.authenticationData에 넣는다.</summary>
    // shortcut: Authenticator가 없으면 연결 ID를 임시 계정 키로 쓴다. DB 로그인(GPT-20261012-03) 후 Authenticator를 붙이면 자동으로 로그인 필수가 된다.
    public string GetAccountKey(NetworkConnectionToClient conn)
    {
        if (conn.authenticationData is PlayerProfile profile) return string.IsNullOrEmpty(profile.userId) ? null : profile.userId;
        if (conn.authenticationData is string accountKey) return accountKey;
        return authenticator == null ? "guest:" + conn.connectionId : null;
    }

    public bool TryGetMatch(NetworkConnectionToClient conn, out Guid matchId) => connectionMatch.TryGetValue(conn, out matchId);

    /// <summary>[클라] 매칭 신청. 결과는 QueueResultReceived.</summary>
    public void ClientJoinQueue(int matchSize) => NetworkClient.Send(new QueueJoinRequest { requestId = ++nextRequestId, matchSize = matchSize });

    /// <summary>[클라] 매칭 취소. 결과는 QueueResultReceived.</summary>
    public void ClientCancelQueue() => NetworkClient.Send(new QueueCancelRequest { requestId = ++nextRequestId });

    /// <summary>[서버] 매칭 신청.</summary>
    [Server]
    public RejectReason JoinQueue(NetworkConnectionToClient conn, QueueJoinRequest request)
    {
        if (!conn.isAuthenticated || GetAccountKey(conn) == null) return RejectReason.NotLoggedIn;
        if (!LexioProtocol.IsValidMatchSize(request.matchSize)) return RejectReason.InvalidMatchSize;
        if (starting.Contains(conn)) return RejectReason.AlreadyInMatch;
        if (HasPendingSettlement(GetAccountKey(conn))) return RejectReason.SettlementPending;
        if (queuedSize.ContainsKey(conn)) return RejectReason.AlreadyQueued; // 모드 변경은 취소 후 신청
        // 잔액이 입장료보다 적어도 매칭 가능 (2026-10-21 사용자 결정. 차감 후 잔액은 음수가 될 수 있음)
        if (connectionMatch.ContainsKey(conn) && !LeaveFinishedMatch(conn)) return RejectReason.AlreadyInMatch;

        queues[request.matchSize].Add(conn);
        queuedSize[conn] = request.matchSize;
        SendQueueUpdated(request.matchSize);
        TryStartMatches();
        return RejectReason.None;
    }

    /// <summary>[서버] 매칭 취소. 시작 확정 후에는 거절.</summary>
    [Server]
    public RejectReason CancelQueue(NetworkConnectionToClient conn, QueueCancelRequest request)
    {
        if (connectionMatch.ContainsKey(conn) || starting.Contains(conn)) return RejectReason.AlreadyInMatch;
        if (!RemoveFromQueue(conn)) return RejectReason.NotQueued;
        return RejectReason.None;
    }

    void OnQueueJoinRequest(NetworkConnectionToClient conn, QueueJoinRequest request) =>
        conn.Send(Result(request.requestId, JoinQueue(conn, request)));

    void OnQueueCancelRequest(NetworkConnectionToClient conn, QueueCancelRequest request) =>
        conn.Send(Result(request.requestId, CancelQueue(conn, request)));

    static RequestResult Result(int requestId, RejectReason reason) =>
        new RequestResult { requestId = requestId, accepted = reason == RejectReason.None, reason = reason };

    // 종료(Finished)된 경기에서만 접속을 유지한 채 빠진다. 남은 사람의 결과 화면이 지워지지 않도록 KPJ ServerRemovePlayer는 부르지 않는다.
    // 플레이어 파괴 → MatchInterestManagement가 관전 목록을 다시 만들어 이 클라에서 이전 테이블이 사라진다.
    bool LeaveFinishedMatch(NetworkConnectionToClient conn)
    {
        Guid matchId = connectionMatch[conn];
        if (!matchTables.TryGetValue(matchId, out LexioTable table) || table == null || table.Phase != GamePhase.Finished) return false;

        List<NetworkConnectionToClient> seats = matches[matchId];
        seats[seats.IndexOf(conn)] = null;
        connectionMatch.Remove(conn);
        NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.Destroy);

        if (seats.TrueForAll(seatConn => seatConn == null))
            EndMatch(matchId);
        return true;
    }

    // 경기 참가자가 모두 나가면 테이블을 지우고 슬롯을 비운다.
    void EndMatch(Guid matchId)
    {
        if (!matches.TryGetValue(matchId, out List<NetworkConnectionToClient> seats)) return;
        TrySaveMatch(matchId); // 결과 확정 직후 전원이 나가도 저장을 잃지 않게 테이블 파괴 전에 확인
        // 미완료(전원 이탈 등)는 Aborted: 입장료 유지, 완료 정산 없음
        if (settlements.TryGetValue(matchId.ToString("D"), out Settlement settlement) && settlement.state == SettlementStarted)
        {
            settlements.Remove(settlement.matchId);
            SaveJournal();
        }
        foreach (NetworkConnectionToClient seatConn in seats)
            if (seatConn != null) connectionMatch.Remove(seatConn);
        matches.Remove(matchId);

        if (matchTables.TryGetValue(matchId, out LexioTable table) && table != null)
            NetworkServer.Destroy(table.gameObject);
        matchTables.Remove(matchId);

        TryStartMatches(); // 슬롯이 비었으니 대기 중인 정원 확인
    }

    bool RemoveFromQueue(NetworkConnectionToClient conn)
    {
        if (!queuedSize.TryGetValue(conn, out int matchSize)) return false;
        queues[matchSize].Remove(conn);
        queuedSize.Remove(conn);
        SendQueueUpdated(matchSize);
        return true;
    }

    void SendQueueUpdated(int matchSize)
    {
        List<NetworkConnectionToClient> queue = queues[matchSize];
        for (int i = 0; i < queue.Count; i++)
        {
            int first = i / matchSize * matchSize;
            int count = Math.Min(matchSize, queue.Count - first);
            string[] nicknames = new string[count];
            int[] profileImages = new int[count];
            nicknames[0] = QueueNickname(queue[i]);
            profileImages[0] = QueueProfileImage(queue[i]);
            int index = 1;
            for (int j = first; j < first + count; j++)
                if (j != i) { nicknames[index] = QueueNickname(queue[j]); profileImages[index++] = QueueProfileImage(queue[j]); }
            queue[i].Send(new QueueUpdated { matchSize = matchSize, waitingCount = count, nicknames = nicknames, profileImages = profileImages });
        }
    }

    static string QueueNickname(NetworkConnectionToClient conn) =>
        conn.authenticationData is PlayerProfile profile ? profile.nickname : "게스트";

    static int QueueProfileImage(NetworkConnectionToClient conn) =>
        conn.authenticationData is PlayerProfile profile ? profile.profileImage : 0;

    public void RefreshQueueProfile(NetworkConnectionToClient conn)
    {
        if (queuedSize.TryGetValue(conn, out int size)) SendQueueUpdated(size);
    }

    // 먼저 신청한 순서대로 정원만큼 한 번에 꺼내 경기 시작. 모드끼리 섞지 않는다.
    void TryStartMatches()
    {
        foreach (KeyValuePair<int, List<NetworkConnectionToClient>> queue in queues)
        {
            int matchSize = queue.Key;
            while (queue.Value.Count >= matchSize && matches.Count + startingMatches < maxMatches)
            {
                List<NetworkConnectionToClient> seats = queue.Value.GetRange(0, matchSize); // 좌석 = 등록 순 0..N-1
                queue.Value.RemoveRange(0, matchSize);
                foreach (NetworkConnectionToClient seatConn in seats)
                {
                    queuedSize.Remove(seatConn);
                    starting.Add(seatConn);
                }

                SendQueueUpdated(matchSize);
                ChargeAndStart(Guid.NewGuid(), matchSize, seats); // 경기ID는 차감 전에 고정
            }
        }
    }

    // 참가자 전원 입장료를 같은 matchId 원장으로 한 트랜잭션 차감한 뒤에만 시작한다(음수 잔액 허용).
    // 시작하지 못하면 같은 matchId로 환불(차감이 없었으면 아무것도 안 함). 실패 계정은 거절 알림, 나머지는 원래 순서로 대기열 앞에 되돌린다.
    // 환불이 DB 오류로 미뤄지면 재시도 목록에 넣고, 정산이 끝날 때까지 해당 계정의 매칭을 막는다.
    async void ChargeAndStart(Guid matchId, int matchSize, List<NetworkConnectionToClient> seats)
    {
        startingMatches++;
        var accounts = authenticator as NYMAccountAuthenticator; // shortcut: NYM 인증기가 없는 테스트 서버는 입장료 없이 시작
        string[] userIds = seats.ConvertAll(GetAccountKey).ToArray();
        RejectReason[] reasons = null;
        PlayerProfile[] profiles = null;
        Settlement settlement = null;
        if (accounts != null)
        {
            // 차감 전에 저널에 남겨, 차감 커밋 직후 서버가 꺼져도 다음 시작 때 환불한다
            settlement = new Settlement { matchId = matchId.ToString("D"), state = SettlementStarting, matchSize = matchSize, userIds = userIds };
            settlements[settlement.matchId] = settlement;
            if (SaveJournal()) (reasons, profiles) = await accounts.ChargeEntryFee(matchId, userIds);
            else reasons = Array.ConvertAll(userIds, _ => RejectReason.ServiceUnavailable);
        }
        startingMatches--;
        if (!NetworkServer.active) return; // 서버 정지: 디스크 저널의 Starting을 다음 시작 때 환불

        bool charged = reasons == null || Array.TrueForAll(reasons, reason => reason == RejectReason.None);
        if (charged && seats.TrueForAll(IsConnected))
        {
            if (settlement != null)
            {
                settlement.state = SettlementStarted;
                settlement.images = Array.ConvertAll(profiles, profile => profile.profileImage); // 차감 커밋 시점 DB 색상
            }
            if ((settlement == null || SaveJournal()) && TryStartMatch(matchId, seats))
            {
                foreach (NetworkConnectionToClient seatConn in seats) starting.Remove(seatConn);
                if (profiles != null)
                    for (int i = 0; i < seats.Count; i++) accounts.SendProfile(seats[i], profiles[i]);
                return;
            }
            if (settlement != null) settlement.state = SettlementStarting;
        }

        bool refunded = true;
        if (settlement != null)
        {
            SettlementOutcome refund = await accounts.RefundEntryFee(matchId, userIds);
            if (!NetworkServer.active) return;
            refunded = refund == SettlementOutcome.Applied;
            if (refunded) settlements.Remove(settlement.matchId);
            else retries[settlement.matchId] = (Time.realtimeSinceStartup + RetryDelay(1), 1);
            SaveJournal();
        }

        var back = new List<NetworkConnectionToClient>();
        for (int i = 0; i < seats.Count; i++)
        {
            starting.Remove(seats[i]);
            if (!IsConnected(seats[i])) continue;
            RejectReason reason = !refunded ? RejectReason.SettlementPending : reasons != null ? reasons[i] : RejectReason.None;
            if (reason != RejectReason.None) seats[i].Send(Result(0, reason)); // requestId 0 = 서버 통보
            else { back.Add(seats[i]); queuedSize[seats[i]] = matchSize; }
        }
        queues[matchSize].InsertRange(0, back);
        SendQueueUpdated(matchSize);
        TryStartMatches();
    }

    // 테이블·플레이어 생성 실패는 시작 전 실패로 보고 만든 것을 지운다(호출자가 환불).
    bool TryStartMatch(Guid matchId, List<NetworkConnectionToClient> seats)
    {
        matches[matchId] = seats;
        foreach (NetworkConnectionToClient seatConn in seats) connectionMatch[seatConn] = matchId;
        try
        {
            StartMatch(matchId, seats);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            foreach (NetworkConnectionToClient seatConn in seats)
            {
                connectionMatch.Remove(seatConn);
                if (seatConn.identity != null) NetworkServer.RemovePlayerForConnection(seatConn, RemovePlayerOptions.Destroy);
            }
            matches.Remove(matchId);
            if (matchTables.TryGetValue(matchId, out LexioTable table) && table != null) NetworkServer.Destroy(table.gameObject);
            matchTables.Remove(matchId);
            return false;
        }
    }

    // KPJ 테이블이 Finished가 되면 한 번 결과를 확정해 저널에 남기고 저장 재시도 목록에 넣는다.
    // 경기 중 이탈 좌석(KPJ isAi)은 칩 고정·순위/저장 제외. 유효 참가자 2명 이하는 Aborted(입장료 유지, 저장 없음).
    // 순위는 최종 칩 기준 경쟁 순위(같은 칩=같은 순위, 공동1등 모두 1등).
    void TrySaveMatch(Guid matchId)
    {
        if (!settlements.TryGetValue(matchId.ToString("D"), out Settlement settlement) || settlement.state != SettlementStarted ||
            !matchTables.TryGetValue(matchId, out LexioTable table) || table == null || table.Phase != GamePhase.Finished) return;
        string[] userIds = settlement.userIds;

        IReadOnlyList<SeatInfo> seatInfos = table.Seats;
        var valid = new List<int>();
        for (int i = 0; i < userIds.Length && i < seatInfos.Count; i++)
            if (!seatInfos[i].isAi && userIds[i] != null) valid.Add(i);
        if (valid.Count <= 2)
        {
            settlements.Remove(settlement.matchId);
            SaveJournal();
            return;
        }

        string[] ids = new string[valid.Count];
        int[] ranks = new int[valid.Count], finalChips = new int[valid.Count];
        for (int v = 0; v < valid.Count; v++)
        {
            int chips = seatInfos[valid[v]].chips;
            ids[v] = userIds[valid[v]];
            // 남은 칩 전부를 잔액에 더함(시작 때 입장료로 차감한 만큼 칩으로 돌려받는 셈, 음수면 그만큼 차감) (2026-10-21 사용자 결정)
            finalChips[v] = chips;
            ranks[v] = 1 + valid.FindAll(other => seatInfos[other].chips > chips).Count;
        }
        settlement.state = SettlementFinished;
        settlement.userIds = ids;
        settlement.ranks = ranks;
        settlement.finalChips = finalChips;
        settlement.seatIndexes = valid.ToArray();
        settlement.finishedAtUtcTicks = DateTime.UtcNow.Ticks; // 재시도도 같은 시각으로 저장
        SaveJournal();
        retries[settlement.matchId] = (0f, 0);
    }

    bool HasPendingSettlement(string accountKey)
    {
        foreach (string key in retries.Keys)
            if (Array.IndexOf(settlements[key].userIds, accountKey) >= 0) return true;
        return false;
    }

    static float RetryDelay(int attempts) => Mathf.Min(60f, Mathf.Pow(2f, attempts));

    void ProcessRetries()
    {
        if (retries.Count == 0 || !(authenticator is NYMAccountAuthenticator accounts)) return;
        foreach (KeyValuePair<string, (float at, int attempts)> retry in new List<KeyValuePair<string, (float at, int attempts)>>(retries))
            if (!settling.Contains(retry.Key) && Time.realtimeSinceStartup >= retry.Value.at) RunSettlement(retry.Key, accounts);
    }

    // 같은 matchId·같은 결과·같은 시각으로 다시 보낸다. DB 원장이 이미 반영된 정산은 AlreadyApplied로 끝난다(중복 잔액·통계·전적 없음).
    async void RunSettlement(string key, NYMAccountAuthenticator accounts)
    {
        Settlement settlement = settlements[key];
        settling.Add(key);
        SettlementOutcome outcome;
        PlayerProfile[] profiles = null;
        if (settlement.state == SettlementFinished)
            (outcome, profiles) = await accounts.SaveMatch(Guid.Parse(key), settlement.matchSize, settlement.userIds, settlement.ranks, settlement.finalChips,
                settlement.seatIndexes, settlement.images, new DateTime(settlement.finishedAtUtcTicks, DateTimeKind.Utc));
        else outcome = await accounts.RefundEntryFee(Guid.Parse(key), settlement.userIds);
        settling.Remove(key);
        if (!NetworkServer.active || !retries.TryGetValue(key, out (float at, int attempts) retry)) return; // 서버 정지: 디스크 저널에서 다음 시작 때 이어감

        if (outcome == SettlementOutcome.Unavailable)
        {
            retries[key] = (Time.realtimeSinceStartup + RetryDelay(retry.attempts + 1), retry.attempts + 1);
            if (retry.attempts % 10 == 0) Debug.LogWarning("[LexioNetworkManager] 정산 DB 오류, 재시도 대기: " + key);
            return;
        }
        retries.Remove(key);
        if (outcome == SettlementOutcome.Rejected)
        {
            settlement.state = SettlementFailed;
            Debug.LogError("[LexioNetworkManager] 정산 거절(재시도 안 함, 저널 Failed 보관): " + key + " " + string.Join(",", settlement.userIds));
        }
        else settlements.Remove(key);
        SaveJournal();

        if (profiles == null) return;
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            int index = Array.IndexOf(settlement.userIds, GetAccountKey(conn));
            if (index >= 0) accounts.SendProfile(conn, profiles[index]);
        }
    }

    string JournalPath => string.IsNullOrEmpty(settlementJournalPath) ? Path.Combine(Application.persistentDataPath, "settlement-journal.json") : settlementJournalPath;

    // 서버 시작 때 미완 정산 복구: Starting=시작 못 함→환불 1회, Started=경기 중 중단→Aborted(입장료 유지), Finished=완료 저장 재시도, Failed=보관.
    void LoadJournal()
    {
        settlements.Clear();
        retries.Clear();
        settling.Clear();
        journalReadable = true;
        if (!(authenticator is NYMAccountAuthenticator)) return;
        try
        {
            if (File.Exists(JournalPath))
                foreach (Settlement settlement in JsonUtility.FromJson<SettlementJournal>(File.ReadAllText(JournalPath)).entries)
                {
                    if (settlement.state == SettlementStarted) continue;
                    settlements[settlement.matchId] = settlement;
                    if (settlement.state != SettlementFailed) retries[settlement.matchId] = (0f, 0);
                }
        }
        catch (Exception exception)
        {
            journalReadable = false;
            Debug.LogError("[LexioNetworkManager] 정산 저널을 읽지 못해 경기 시작을 막습니다. 파일 확인 필요: " + JournalPath + " " + exception.Message);
            return;
        }
        SaveJournal();
    }

    // 임시 파일에 쓴 뒤 교체(쓰기 도중 종료돼도 이전 저널 유지).
    bool SaveJournal()
    {
        if (!journalReadable) return false;
        try
        {
            var journal = new SettlementJournal();
            journal.entries.AddRange(settlements.Values);
            string path = JournalPath, temp = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(temp, JsonUtility.ToJson(journal));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("[LexioNetworkManager] 정산 저널 저장 실패: " + exception.Message);
            return false;
        }
    }

    // 경기 중 남은 사람이 2명 이하 → Aborted: 입장료 유지, 완료 정산·판수·전적 없음. 남은 사람은 접속을 유지한 채 메인으로 돌아가 다시 매칭한다.
    void AbortMatch(Guid matchId)
    {
        if (settlements.Remove(matchId.ToString("D"))) SaveJournal();
        List<NetworkConnectionToClient> seats = matches[matchId];
        for (int i = 0; i < seats.Count; i++)
        {
            NetworkConnectionToClient seatConn = seats[i];
            if (seatConn == null) continue;
            seats[i] = null;
            connectionMatch.Remove(seatConn);
            seatConn.Send(Result(0, RejectReason.MatchEnded)); // requestId 0 = 서버 통보
            NetworkServer.RemovePlayerForConnection(seatConn, RemovePlayerOptions.Destroy);
        }
        EndMatch(matchId);
    }

    static bool IsConnected(NetworkConnectionToClient conn) =>
        NetworkServer.active && NetworkServer.connections.TryGetValue(conn.connectionId, out NetworkConnectionToClient current) && current == conn;

    // 같은 matchId를 테이블·플레이어 NetworkMatch에 스폰 전에 넣어 MatchInterestManagement로 경기끼리 격리한다.
    void StartMatch(Guid matchId, List<NetworkConnectionToClient> seats)
    {
        LexioTable table = Instantiate(matchTablePrefab);
        table.GetComponent<NetworkMatch>().matchId = matchId;
        NetworkServer.Spawn(table.gameObject);
        matchTables[matchId] = table;

        List<LexioPlayer> players = new List<LexioPlayer>(seats.Count);
        foreach (NetworkConnectionToClient seatConn in seats)
        {
            GameObject playerObject = Instantiate(playerPrefab);
            playerObject.name = $"{playerPrefab.name} [connId={seatConn.connectionId}]";
            playerObject.GetComponent<NetworkMatch>().matchId = matchId;
            NetworkServer.AddPlayerForConnection(seatConn, playerObject);
            players.Add(playerObject.GetComponent<LexioPlayer>());
        }

        foreach (LexioPlayer player in players)
            table.ServerAddPlayer(player); // 5인은 마지막 입장 때 KPJ 테이블이 바로 시작
        table.ServerRequestStart(players[0]); // 3·4인 시작. 이미 시작했으면 무시됨
    }

    /// <summary>
    /// Runs on both Server and Client
    /// Networking is NOT initialized when this fires
    /// </summary>
    public override void Awake()
    {
        base.Awake();
    }

    #region Unity Callbacks

    public override void OnValidate()
    {
        base.OnValidate();
    }

    /// <summary>
    /// Runs on both Server and Client
    /// Networking is NOT initialized when this fires
    /// </summary>
    public override void Start()
    {
        base.Start();
    }

    /// <summary>
    /// Runs on both Server and Client
    /// </summary>
    public override void LateUpdate()
    {
        base.LateUpdate();
        if (!NetworkServer.active) return;
        if (matchTables.Count > 0)
            foreach (Guid matchId in new List<Guid>(matchTables.Keys)) TrySaveMatch(matchId);
        ProcessRetries();
    }

    /// <summary>
    /// Runs on both Server and Client
    /// </summary>
    public override void OnDestroy()
    {
        base.OnDestroy();
    }

    #endregion

    #region Start & Stop

    /// <summary>
    /// Set the frame rate for a headless server.
    /// <para>Override if you wish to disable the behavior or set your own tick rate.</para>
    /// </summary>
    public override void ConfigureHeadlessFrameRate()
    {
        base.ConfigureHeadlessFrameRate();
    }

    /// <summary>
    /// called when quitting the application by closing the window / pressing stop in the editor
    /// </summary>
    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
    }

    #endregion

    #region Scene Management

    /// <summary>
    /// This causes the server to switch scenes and sets the networkSceneName.
    /// <para>Clients that connect to this server will automatically switch to this scene. This is called automatically if onlineScene or offlineScene are set, but it can be called from user code to switch scenes again while the game is in progress. This automatically sets clients to be not-ready. The clients must call NetworkClient.Ready() again to participate in the new scene.</para>
    /// </summary>
    /// <param name="newSceneName"></param>
    public override void ServerChangeScene(string newSceneName)
    {
        base.ServerChangeScene(newSceneName);
    }

    /// <summary>
    /// Called from ServerChangeScene immediately before SceneManager.LoadSceneAsync is executed
    /// <para>This allows server to do work / cleanup / prep before the scene changes.</para>
    /// </summary>
    /// <param name="newSceneName">Name of the scene that's about to be loaded</param>
    public override void OnServerChangeScene(string newSceneName) { }

    /// <summary>
    /// Called on the server when a scene is completed loaded, when the scene load was initiated by the server with ServerChangeScene().
    /// </summary>
    /// <param name="sceneName">The name of the new scene.</param>
    public override void OnServerSceneChanged(string sceneName) { }

    /// <summary>
    /// Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed
    /// <para>This allows client to do work / cleanup / prep before the scene changes.</para>
    /// </summary>
    /// <param name="newSceneName">Name of the scene that's about to be loaded</param>
    /// <param name="sceneOperation">Scene operation that's about to happen</param>
    /// <param name="customHandling">true to indicate that scene loading will be handled through overrides</param>
    public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling) { }

    /// <summary>
    /// Called on clients when a scene has completed loaded, when the scene load was initiated by the server.
    /// <para>Scene changes can cause player objects to be destroyed. The default implementation of OnClientSceneChanged in the NetworkManager is to add a player object for the connection if no player object exists.</para>
    /// </summary>
    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();
    }

    #endregion

    #region Server System Callbacks

    /// <summary>
    /// Called on the server when a new client connects.
    /// <para>Unity calls this on the Server when a Client connects to the Server. Use an override to tell the NetworkManager what to do when a client connects to the server.</para>
    /// </summary>
    /// <param name="conn">Connection from client.</param>
    // KPJ base는 단일 테이블이 없으면 끊으므로 호출하지 않는다. 접속자는 대기열 신청 전까지 플레이어 없이 대기.
    public override void OnServerConnect(NetworkConnectionToClient conn) { }

    /// <summary>
    /// Called on the server when a client is ready.
    /// <para>The default implementation of this function calls NetworkServer.SetClientReady() to continue the network setup process.</para>
    /// </summary>
    /// <param name="conn">Connection from client.</param>
    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        base.OnServerReady(conn);
    }

    /// <summary>
    /// Called on the server when a client adds a new player with ClientScene.AddPlayer.
    /// <para>The default implementation for this function creates a new player object from the playerPrefab.</para>
    /// </summary>
    /// <param name="conn">Connection from client.</param>
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // 플레이어는 StartMatch에서만 만든다. 클라가 직접 요청하면 KPJ base가 단일 테이블 없음으로 연결을 끊는다.
        base.OnServerAddPlayer(conn);
    }

    /// <summary>
    /// Called on the server when a client disconnects.
    /// <para>This is called on the Server when a Client disconnects from the Server. Use an override to decide what should happen when a disconnection is detected.</para>
    /// </summary>
    /// <param name="conn">Connection from client.</param>
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (conn == null) return; // 호스트 시작 실패 후 Mirror 정리 콜백
        RemoveFromQueue(conn);

        // 재접속 없음: 좌석은 비워 두고(null) 소속만 끊는다. 남은 판은 KPJ 테이블이 AI로 진행.
        bool isInMatch = connectionMatch.TryGetValue(conn, out Guid matchId);
        if (isInMatch)
        {
            List<NetworkConnectionToClient> seats = matches[matchId];
            seats[seats.IndexOf(conn)] = null;
            connectionMatch.Remove(conn);
        }

        base.OnServerDisconnect(conn); // KPJ: 테이블 좌석을 AI로 전환 후 플레이어 파괴

        if (!isInMatch) return;
        if (matches[matchId].TrueForAll(seatConn => seatConn == null))
            EndMatch(matchId);
        else if (matchTables.TryGetValue(matchId, out LexioTable table) && table != null && table.Phase == GamePhase.Playing && !table.IsGameOver &&
            matches[matchId].FindAll(seatConn => seatConn != null).Count <= 2)
            AbortMatch(matchId);
    }

    /// <summary>
    /// Called on server when transport raises an error.
    /// <para>NetworkConnection may be null.</para>
    /// </summary>
    /// <param name="conn">Connection of the client...may be null</param>
    /// <param name="transportError">TransportError enum</param>
    /// <param name="message">String message of the error.</param>
    public override void OnServerError(NetworkConnectionToClient conn, TransportError transportError, string message) { }

    /// <summary>
    /// Called on server when transport raises an exception.
    /// <para>NetworkConnection may be null.</para>
    /// </summary>
    /// <param name="conn">Connection of the client...may be null</param>
    /// <param name="exception">Exception thrown from the Transport.</param>
    public override void OnServerTransportException(NetworkConnectionToClient conn, Exception exception) { }

    #endregion

    #region Client System Callbacks

    /// <summary>
    /// Called on the client when connected to a server.
    /// <para>The default implementation of this function sets the client as ready and adds a player. Override the function to dictate what happens when the client connects.</para>
    /// </summary>
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        if (autoJoinMatchSize != 0) ClientJoinQueue(autoJoinMatchSize);
    }

    /// <summary>
    /// Called on clients when disconnected from a server.
    /// <para>This is called on the client when it disconnects from the server. Override this function to decide what happens when the client disconnects.</para>
    /// </summary>
    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect(); // KPJ ClientDisconnected 이벤트
    }

    /// <summary>
    /// Called on clients when a servers tells the client it is no longer ready.
    /// <para>This is commonly used when switching scenes.</para>
    /// </summary>
    public override void OnClientNotReady() { }

    /// <summary>
    /// Called on client when transport raises an error.</summary>
    /// </summary>
    /// <param name="transportError">TransportError enum.</param>
    /// <param name="message">String message of the error.</param>
    public override void OnClientError(TransportError transportError, string message) { }

    /// <summary>
    /// Called on client when transport raises an exception.</summary>
    /// </summary>
    /// <param name="exception">Exception thrown from the Transport.</param>
    public override void OnClientTransportException(Exception exception) { }

    #endregion

    #region Start & Stop Callbacks

    // Since there are multiple versions of StartServer, StartClient and StartHost, to reliably customize
    // their functionality, users would need override all the versions. Instead these callbacks are invoked
    // from all versions, so users only need to implement this one case.

    /// <summary>
    /// This is invoked when a host is started.
    /// <para>StartHost has multiple signatures, but they all cause this hook to be called.</para>
    /// </summary>
    public override void OnStartHost() { }

    /// <summary>
    /// This is invoked when a server is started - including when a host is started.
    /// <para>StartServer has multiple signatures, but they all cause this hook to be called.</para>
    /// </summary>
    public override void OnStartServer()
    {
        // KPJ base(단일 테이블 생성)는 호출하지 않는다. 테이블은 경기마다 StartMatch에서 만든다.
        if (matchTablePrefab == null || !matchTablePrefab.TryGetComponent(out NetworkMatch _) ||
            playerPrefab == null || !playerPrefab.TryGetComponent(out NetworkMatch _))
        {
            Debug.LogError("[LexioNetworkManager] matchTablePrefab·Player Prefab에 NetworkMatch가 있어야 합니다. 매칭 비활성");
            return;
        }

        NetworkServer.RegisterHandler<QueueJoinRequest>(OnQueueJoinRequest);
        NetworkServer.RegisterHandler<QueueCancelRequest>(OnQueueCancelRequest);
        LoadJournal();
    }

    /// <summary>
    /// This is invoked when the client is started.
    /// </summary>
    public override void OnStartClient()
    {
        NetworkClient.RegisterHandler<RequestResult>(result => QueueResultReceived?.Invoke(result));
        NetworkClient.RegisterHandler<QueueUpdated>(update => QueueUpdatedReceived?.Invoke(update));
    }

    /// <summary>
    /// This is called when a host is stopped.
    /// </summary>
    public override void OnStopHost() { }

    /// <summary>
    /// This is called when a server is stopped - including when a host is stopped.
    /// </summary>
    public override void OnStopServer()
    {
        foreach (List<NetworkConnectionToClient> queue in queues.Values) queue.Clear();
        queuedSize.Clear();
        starting.Clear(); // startingMatches는 진행 중인 ChargeAndStart가 스스로 줄인다
        matches.Clear();
        connectionMatch.Clear();
        matchTables.Clear();
        // 디스크 저널은 그대로 둔다. 다음 시작 때 Started는 Aborted, 나머지는 이어서 처리
        settlements.Clear();
        retries.Clear();
        base.OnStopServer();
    }

    /// <summary>
    /// This is called when a client is stopped.
    /// </summary>
    public override void OnStopClient() { }

    #endregion
}
