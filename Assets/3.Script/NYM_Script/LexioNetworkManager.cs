using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

/*
	Documentation: https://mirror-networking.gitbook.io/docs/components/network-manager
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkManager.html
*/

public class LexioNetworkManager : NetworkManager
{
    // Overrides the base singleton so we don't
    // have to cast to this type everywhere.
    public static new LexioNetworkManager singleton => (LexioNetworkManager)NetworkManager.singleton;

    [Header("렉시오 서버 설정")]
    [Tooltip("이 서버에서 동시에 진행할 수 있는 최대 경기 수. 꽉 차면 정원이 모여도 빈 경기 슬롯이 생길 때까지 대기")]
    [SerializeField] int maxMatches = 20;

    // ───────── 서버 전용 상태 (매칭·경기 소속) ─────────
    // 경기 상태(손패·점수 등)는 GameSession이 보관한다. 여기는 연결 ↔ 대기열/경기 소속만 관리.

    readonly Dictionary<int, List<NetworkConnectionToClient>> queues = new Dictionary<int, List<NetworkConnectionToClient>>
    {
        { 3, new List<NetworkConnectionToClient>() },
        { 4, new List<NetworkConnectionToClient>() },
        { 5, new List<NetworkConnectionToClient>() }
    };
    readonly Dictionary<NetworkConnectionToClient, int> queuedSize = new Dictionary<NetworkConnectionToClient, int>();
    readonly Dictionary<Guid, List<NetworkConnectionToClient>> matches = new Dictionary<Guid, List<NetworkConnectionToClient>>();
    readonly Dictionary<NetworkConnectionToClient, Guid> connectionMatch = new Dictionary<NetworkConnectionToClient, Guid>();

    /// <summary>대기 인원 변경 (matchSize, 대기 연결 목록). 플레이어 오브젝트가 QueueUpdated를 TargetRpc로 보낸다.</summary>
    public event Action<int, IReadOnlyList<NetworkConnectionToClient>> QueueChanged;

    /// <summary>경기 시작 확정 (matchId, matchSize, 좌석순 연결). 여기서 GameSession·테이블을 만들고 MatchFound를 보낸다.</summary>
    public event Action<Guid, int, IReadOnlyList<NetworkConnectionToClient>> MatchStarted;

    /// <summary>경기 중 이탈 (matchId, 좌석). 플레이어 오브젝트가 파괴되기 전에 호출된다. GameSession.OnDisconnected로 전달.</summary>
    public event Action<Guid, int> MatchPlayerLeft;

    /// <summary>로그인한 연결의 계정 키. AccountAuthenticator가 인증 성공 시 conn.authenticationData에 넣는다.</summary>
    public static string GetAccountKey(NetworkConnectionToClient conn) => conn.authenticationData as string;

    public bool TryGetMatch(NetworkConnectionToClient conn, out Guid matchId) => connectionMatch.TryGetValue(conn, out matchId);

    /// <summary>[서버] 매칭 신청. 플레이어의 Command에서 호출.</summary>
    [Server]
    public RejectReason JoinQueue(NetworkConnectionToClient conn, QueueJoinRequest request)
    {
        if (GetAccountKey(conn) == null) return RejectReason.NotLoggedIn;
        if (!LexioProtocol.IsValidMatchSize(request.matchSize)) return RejectReason.InvalidMatchSize;
        if (connectionMatch.ContainsKey(conn)) return RejectReason.AlreadyInMatch;
        if (queuedSize.ContainsKey(conn)) return RejectReason.AlreadyQueued; // 모드 변경은 취소 후 신청

        queues[request.matchSize].Add(conn);
        queuedSize[conn] = request.matchSize;
        QueueChanged?.Invoke(request.matchSize, queues[request.matchSize]);
        TryStartMatches();
        return RejectReason.None;
    }

    /// <summary>[서버] 매칭 취소. 시작 확정 후에는 거절.</summary>
    [Server]
    public RejectReason CancelQueue(NetworkConnectionToClient conn, QueueCancelRequest request)
    {
        if (connectionMatch.ContainsKey(conn)) return RejectReason.AlreadyInMatch;
        if (!RemoveFromQueue(conn)) return RejectReason.NotQueued;
        return RejectReason.None;
    }

    /// <summary>[서버] 경기 종료(GameEnd·Aborted) 후 GameSession이 호출. 남은 참가자는 새 매칭 가능.</summary>
    [Server]
    public void EndMatch(Guid matchId)
    {
        if (!matches.TryGetValue(matchId, out List<NetworkConnectionToClient> seats)) return;
        foreach (NetworkConnectionToClient seatConn in seats)
            if (seatConn != null) connectionMatch.Remove(seatConn);
        matches.Remove(matchId);
        TryStartMatches(); // 슬롯이 비었으니 대기 중인 정원 확인
    }

    bool RemoveFromQueue(NetworkConnectionToClient conn)
    {
        if (!queuedSize.TryGetValue(conn, out int matchSize)) return false;
        queues[matchSize].Remove(conn);
        queuedSize.Remove(conn);
        QueueChanged?.Invoke(matchSize, queues[matchSize]);
        return true;
    }

    // 먼저 신청한 순서대로 정원만큼 한 번에 꺼내 경기 시작. 모드끼리 섞지 않는다.
    void TryStartMatches()
    {
        foreach (KeyValuePair<int, List<NetworkConnectionToClient>> queue in queues)
        {
            int matchSize = queue.Key;
            while (queue.Value.Count >= matchSize && matches.Count < maxMatches)
            {
                List<NetworkConnectionToClient> seats = queue.Value.GetRange(0, matchSize); // 좌석 = 등록 순 0..N-1
                queue.Value.RemoveRange(0, matchSize);

                Guid matchId = Guid.NewGuid();
                matches[matchId] = seats;
                foreach (NetworkConnectionToClient seatConn in seats)
                {
                    queuedSize.Remove(seatConn);
                    connectionMatch[seatConn] = matchId;
                }

                QueueChanged?.Invoke(matchSize, queue.Value);
                MatchStarted?.Invoke(matchId, matchSize, seats);
            }
        }
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
        base.OnServerAddPlayer(conn);
    }

    /// <summary>
    /// Called on the server when a client disconnects.
    /// <para>This is called on the Server when a Client disconnects from the Server. Use an override to decide what should happen when a disconnection is detected.</para>
    /// </summary>
    /// <param name="conn">Connection from client.</param>
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        RemoveFromQueue(conn);

        // 재접속 없음: 좌석은 비워 두고(null) 소속만 끊는다. 좌석·손패·점수는 GameSession에 남는다.
        if (connectionMatch.TryGetValue(conn, out Guid matchId))
        {
            List<NetworkConnectionToClient> seats = matches[matchId];
            int seat = seats.IndexOf(conn);
            seats[seat] = null;
            connectionMatch.Remove(conn);
            MatchPlayerLeft?.Invoke(matchId, seat);
        }

        base.OnServerDisconnect(conn);
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
    }

    /// <summary>
    /// Called on clients when disconnected from a server.
    /// <para>This is called on the client when it disconnects from the server. Override this function to decide what happens when the client disconnects.</para>
    /// </summary>
    public override void OnClientDisconnect() { }

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
    public override void OnStartServer() { }

    /// <summary>
    /// This is invoked when the client is started.
    /// </summary>
    public override void OnStartClient() { }

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
        matches.Clear();
        connectionMatch.Clear();
    }

    /// <summary>
    /// This is called when a client is stopped.
    /// </summary>
    public override void OnStopClient() { }

    #endregion
}
