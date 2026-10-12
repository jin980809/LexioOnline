using Mirror;

// 접속한 클라이언트 1명. 입력(Command)과 개인 정보(손패 TargetRpc) 전달 담당
// 클라이언트 쪽 수신 내용은 LexioNetworkManager로 넘겨서 UI/뷰가 받음
public class LexioPlayer : NetworkBehaviour
{
    public const int MaxNicknameLength = 12;

    [SyncVar] private int seat = -1;

    public int Seat
    {
        get { return seat; }
    }

    public LexioTable Table { get; set; }

    [Server]
    public void SetSeat(int newSeat)
    {
        seat = newSeat;
    }

    public override void OnStartLocalPlayer()
    {
        LexioNetworkManager manager = GetManager();
        if (manager != null)
        {
            manager.RegisterLocalPlayer(this);
            CmdSetNickname(manager.PendingNickname);
        }
    }

    public override void OnStopLocalPlayer()
    {
        LexioNetworkManager manager = GetManager();
        if (manager != null)
        {
            manager.UnregisterLocalPlayer(this);
        }
    }

    [Command]
    private void CmdSetNickname(string nickname)
    {
        if (Table != null)
        {
            Table.ServerSetNickname(this, nickname);
        }
    }

    [Command]
    public void CmdRequestStart()
    {
        if (Table != null)
        {
            Table.ServerRequestStart(this);
        }
    }

    [Command]
    public void CmdReady()
    {
        if (Table != null)
        {
            Table.ServerReady(this);
        }
    }

    [Command]
    public void CmdSubmit(byte[] tileIds)
    {
        if (Table != null)
        {
            Table.ServerSubmit(this, tileIds);
        }
    }

    [Command]
    public void CmdPass()
    {
        if (Table != null)
        {
            Table.ServerPass(this);
        }
    }

    [TargetRpc]
    public void TargetSetHand(byte[] tileIds, bool isNewDeal, int mySeat, int playerCount)
    {
        LexioNetworkManager manager = GetManager();
        if (manager != null)
        {
            manager.RaiseHandReceived(tileIds, isNewDeal, mySeat, playerCount);
        }
    }

    [TargetRpc]
    public void TargetNotice(string message)
    {
        LexioNetworkManager manager = GetManager();
        if (manager != null)
        {
            manager.RaiseNotice(message);
        }
    }

    // 스폰된 오브젝트라 씬 참조를 받을 수 없어서 Mirror의 NetworkManager.singleton 사용
    private LexioNetworkManager GetManager()
    {
        return NetworkManager.singleton as LexioNetworkManager;
    }
}
