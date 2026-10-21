using System;
using System.Collections.Generic;
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
    // 시작한 경기의 좌석별 계정·시작 시점 색상 스냅샷(연결이 끊겨도 완료 저장에 필요). 저장을 시작하면 지운다.
    readonly Dictionary<Guid, (string[] userIds, int[] images)> matchAccounts = new Dictionary<Guid, (string[] userIds, int[] images)>();
    // 완료 저장 중인 계정. 저장 커밋 전 재매칭 차감과 순서가 섞이지 않도록 신청을 막는다.
    readonly HashSet<string> saving = new HashSet<string>();

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
        if (starting.Contains(conn) || saving.Contains(GetAccountKey(conn))) return RejectReason.AlreadyInMatch;
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
        matchAccounts.Remove(matchId); // 미완료(전원 이탈 등)는 저장 없음
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

    // 참가자 전원 입장료를 한 트랜잭션으로 차감한 뒤에만 시작한다. 실패하면 아무도 차감되지 않고,
    // 부족/DB 오류 계정은 거절 알림, 나머지는 원래 순서로 대기열 앞에 되돌린다.
    // shortcut: 차감 후 시작 전 연결 해제 시 복구는 메모리 상태 기준 1회 보상 UPDATE. 서버 재시작/복구 실패 대비 영속 원장은 DB-20261020-06 계약 후 추가.
    async void ChargeAndStart(Guid matchId, int matchSize, List<NetworkConnectionToClient> seats)
    {
        startingMatches++;
        var accounts = authenticator as NYMAccountAuthenticator; // shortcut: NYM 인증기가 없는 테스트 서버는 입장료 없이 시작
        string[] userIds = seats.ConvertAll(GetAccountKey).ToArray();
        RejectReason[] reasons = null;
        PlayerProfile[] profiles = null;
        if (accounts != null) (reasons, profiles) = await accounts.ChangeCoins(userIds, -LexioProtocol.EntryFee);

        bool charged = reasons == null || Array.TrueForAll(reasons, reason => reason == RejectReason.None);
        bool[] connected = seats.ConvertAll(IsConnected).ToArray();
        if (charged && Array.TrueForAll(connected, value => value))
        {
            startingMatches--;
            matches[matchId] = seats;
            if (accounts != null) matchAccounts[matchId] = (userIds, Array.ConvertAll(profiles, profile => profile.profileImage)); // 차감 커밋 시점 DB 색상
            for (int i = 0; i < seats.Count; i++)
            {
                starting.Remove(seats[i]);
                connectionMatch[seats[i]] = matchId;
                if (profiles != null) accounts.SendProfile(seats[i], profiles[i]);
            }
            StartMatch(matchId, seats);
            return;
        }

        if (charged && accounts != null)
        {
            (RejectReason[] refund, _) = await accounts.ChangeCoins(userIds, LexioProtocol.EntryFee);
            if (!Array.TrueForAll(refund, reason => reason == RejectReason.None))
                Debug.LogError("[LexioNetworkManager] 입장료 복구 실패: " + string.Join(",", userIds));
            connected = seats.ConvertAll(IsConnected).ToArray();
        }
        startingMatches--;

        var back = new List<NetworkConnectionToClient>();
        for (int i = 0; i < seats.Count; i++)
        {
            starting.Remove(seats[i]);
            if (!connected[i]) continue;
            if (reasons != null && reasons[i] != RejectReason.None) seats[i].Send(Result(0, reasons[i])); // requestId 0 = 서버 통보
            else { back.Add(seats[i]); queuedSize[seats[i]] = matchSize; }
        }
        if (!NetworkServer.active) return;
        queues[matchSize].InsertRange(0, back);
        SendQueueUpdated(matchSize);
        TryStartMatches();
    }

    // KPJ 테이블이 Finished가 되면 한 번 저장. AI로 바뀐 좌석(경기 중 이탈)은 제외하고, 유효 참가자 2명 이하는 Aborted로 보고 저장하지 않는다.
    // 순위는 최종 칩 기준 경쟁 순위(같은 칩=같은 순위, 공동1등 모두 1등).
    // shortcut: 중복 저장 방지는 matchAccounts 1회 제거(메모리) 기준, 실패 시 재시도 없이 Error 로그. 영속 완료 원장·재시작 복구는 DB-20261020-06 계약 후 추가.
    // shortcut: 이탈자 칩 고정·KPJ Aborted 전환(QA-20261020-02)은 KPJ 담당, 여기서는 저장 제외만 한다.
    void TrySaveMatch(Guid matchId)
    {
        if (!matchAccounts.TryGetValue(matchId, out (string[] userIds, int[] images) snapshot) || !matchTables.TryGetValue(matchId, out LexioTable table) ||
            table == null || table.Phase != GamePhase.Finished || !(authenticator is NYMAccountAuthenticator accounts)) return;
        matchAccounts.Remove(matchId);
        string[] userIds = snapshot.userIds;

        IReadOnlyList<SeatInfo> seatInfos = table.Seats;
        var valid = new List<int>();
        for (int i = 0; i < userIds.Length && i < seatInfos.Count; i++)
            if (!seatInfos[i].isAi && userIds[i] != null) valid.Add(i);
        if (valid.Count <= 2) return;

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
        SaveMatch(userIds.Length, ids, ranks, finalChips, valid.ToArray(), snapshot.images, accounts);
    }

    async void SaveMatch(int matchSize, string[] ids, int[] ranks, int[] finalChips, int[] seatIndexes, int[] images, NYMAccountAuthenticator accounts)
    {
        foreach (string id in ids) saving.Add(id);
        PlayerProfile[] profiles = await accounts.SaveMatch(matchSize, ids, ranks, finalChips, seatIndexes, images, DateTime.UtcNow);
        foreach (string id in ids) saving.Remove(id);
        if (profiles == null) { Debug.LogError("[LexioNetworkManager] 경기 결과 저장 실패: " + string.Join(",", ids)); return; }
        if (!NetworkServer.active) return;
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            int index = Array.IndexOf(ids, GetAccountKey(conn));
            if (index >= 0) accounts.SendProfile(conn, profiles[index]);
        }
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
        if (NetworkServer.active && matchAccounts.Count > 0)
            foreach (Guid matchId in new List<Guid>(matchAccounts.Keys)) TrySaveMatch(matchId);
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

        if (isInMatch && matches[matchId].TrueForAll(seatConn => seatConn == null))
            EndMatch(matchId);
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
        matchAccounts.Clear();
        base.OnStopServer();
    }

    /// <summary>
    /// This is called when a client is stopped.
    /// </summary>
    public override void OnStopClient() { }

    #endregion
}
