using System;
using Mirror;
using UnityEngine;

// 네트워크 시작/접속 처리 + 클라이언트 쪽 연결 허브
// 스폰된 테이블/로컬 플레이어를 여기에 등록하고, UI/뷰는 인스펙터로 이 매니저만 참조함
public class LexioTableNetworkManager : NetworkManager
{
    // Server 씬에서는 true: 실행하면 바로 서버 시작 (에디터/헤드리스 빌드 공통)
    [SerializeField] private bool startAsServer;
    [SerializeField] private LexioTable tablePrefab;

    private LexioTable serverTable;

    // 클라이언트: 현재 테이블과 내 플레이어
    public LexioTable Table { get; private set; }
    public LexioPlayer LocalPlayer { get; private set; }

    // 클라이언트가 접속 전에 입력한 닉네임
    public string PendingNickname { get; set; } = "";

    // 손패 수신 (타일 ID 배열, 새로 받은 패인지, 내 자리, 인원 수)
    // 자리/인원은 SyncVar보다 RPC가 먼저 도착할 수 있어서 손패와 함께 받음
    public event Action<byte[], bool, int, int> HandReceived;
    public event Action<string> NoticeReceived;
    public event Action ClientDisconnected;

    public override void Start()
    {
        base.Start();
        if (startAsServer && NetworkServer.active == false)
        {
            StartServer();
        }
    }

    // ---------------- 클라이언트 등록 ----------------

    public void RegisterTable(LexioTable table)
    {
        Table = table;
    }

    public void UnregisterTable(LexioTable table)
    {
        if (Table == table)
        {
            Table = null;
        }
    }

    public void RegisterLocalPlayer(LexioPlayer player)
    {
        LocalPlayer = player;
    }

    public void UnregisterLocalPlayer(LexioPlayer player)
    {
        if (LocalPlayer == player)
        {
            LocalPlayer = null;
        }
    }

    public void RaiseHandReceived(byte[] tileIds, bool isNewDeal, int mySeat, int playerCount)
    {
        if (HandReceived != null)
        {
            HandReceived(tileIds, isNewDeal, mySeat, playerCount);
        }
    }

    public void RaiseNotice(string message)
    {
        if (NoticeReceived != null)
        {
            NoticeReceived(message);
        }
    }

    // ---------------- 서버 ----------------

    public override void OnStartServer()
    {
        base.OnStartServer();
        serverTable = Instantiate(tablePrefab);
        NetworkServer.Spawn(serverTable.gameObject);
    }

    public override void OnStopServer()
    {
        serverTable = null;
        base.OnStopServer();
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        LexioTable table = FindTableFor(conn);
        if (table == null)
        {
            // 게임 진행 중이거나 인원 초과
            conn.Disconnect();
        }
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        LexioTable table = FindTableFor(conn);
        if (table != null)
        {
            base.OnServerAddPlayer(conn);
            LexioPlayer player = conn.identity.GetComponent<LexioPlayer>();
            table.ServerAddPlayer(player);
        }
        else
        {
            conn.Disconnect();
        }
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (conn.identity != null)
        {
            LexioPlayer player = conn.identity.GetComponent<LexioPlayer>();
            if (player != null && player.Table != null)
            {
                player.Table.ServerRemovePlayer(player);
            }
        }
        base.OnServerDisconnect(conn);
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        if (ClientDisconnected != null)
        {
            ClientDisconnected();
        }
    }

    // 지금은 테이블 1개. 매치메이킹을 붙일 때 이 함수만 매치 배정 로직으로 교체
    private LexioTable FindTableFor(NetworkConnectionToClient conn)
    {
        LexioTable result = null;
        if (serverTable != null && serverTable.CanJoin)
        {
            result = serverTable;
        }
        return result;
    }
}
