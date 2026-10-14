using System.Collections.Generic;
using Mirror;
using UnityEngine;

// 게임 한 판(방) 단위. 서버가 진행을 맡고, 공개 정보만 SyncVar/SyncList로 동기화
// 추후 매치메이킹에서는 매치마다 이 테이블을 하나씩 만들어 플레이어를 배정하면 됨
public class LexioTable : NetworkBehaviour
{
    [SerializeField] private float turnSeconds = 30f;
    [SerializeField] private float aiDelaySeconds = 1f;
    // 라운드 끝 → 다음 라운드(또는 결과 화면)까지 대기. 클라이언트 ChipFlightView.maxSettleSeconds보다 길게 (정산 칩 연출 시간)
    [SerializeField] private float roundEndDelaySeconds = 5f;

    // 라운드 시작 패 돌리기 연출 시간. 클라이언트 LexioClientView.dealSeconds보다 조금 길게 (지연 여유)
    [SerializeField] private float dealSeconds = 2f;

    // 매 라운드 패를 받은 뒤 패를 보고 칸에 배치하는 시간. 이 동안은 아무도 낼 수 없음
    [SerializeField] private float prepareSeconds = 60f;

    // 한 게임에 진행할 라운드 수. 서버 프리팹 값이 기준이고, 클라이언트에는 SyncVar로 전달되어 UI 표시에 쓰임
    [SerializeField, Min(1), SyncVar] private int roundCount = GameRules.RoundCount;

    // 게임 시작 시 각자 받는 칩 개수 (서버 전용, 클라이언트는 SeatInfo.chips로 받음)
    [SerializeField, Min(0)] private int startingChips = GameRules.StartingChips;

    [SyncVar(hook = nameof(OnPhaseChanged))] private GamePhase phase = GamePhase.Waiting;
    [SyncVar(hook = nameof(OnIntChanged))] private int maxNumber;
    [SyncVar(hook = nameof(OnIntChanged))] private int roundNumber;
    [SyncVar(hook = nameof(OnIntChanged))] private int currentSeat = -1;
    [SyncVar(hook = nameof(OnBoolChanged))] private bool isLeadTurn;
    [SyncVar] private double turnDeadline;
    [SyncVar] private double prepareEndTime;
    [SyncVar(hook = nameof(OnIntChanged))] private int lastPlaySeat = -1;

    private readonly SyncList<SeatInfo> seats = new SyncList<SeatInfo>();
    private readonly SyncList<byte> lastPlayTiles = new SyncList<byte>();

    // 서버 전용
    private readonly List<LexioPlayer> seatPlayers = new List<LexioPlayer>();
    private readonly System.Random random = new System.Random();
    private LexioGame game;
    private double turnStartTime;
    private bool isWaitingNextRound;
    private double nextRoundTime;
    private double dealEndTime;

    public GamePhase Phase
    {
        get { return phase; }
    }

    public int MaxNumber
    {
        get { return maxNumber; }
    }

    public int RoundNumber
    {
        get { return roundNumber; }
    }

    public int RoundCount
    {
        get { return roundCount; }
    }

    public int CurrentSeat
    {
        get { return currentSeat; }
    }

    public bool IsLeadTurn
    {
        get { return isLeadTurn; }
    }

    public double TurnDeadline
    {
        get { return turnDeadline; }
    }

    public double PrepareEndTime
    {
        get { return prepareEndTime; }
    }

    // 라운드 시작 후 패 정리 시간 중인지 (서버/클라이언트 모두 NetworkTime 기준)
    public bool IsPreparing
    {
        get { return phase == GamePhase.Playing && NetworkTime.time < prepareEndTime; }
    }

    public int LastPlaySeat
    {
        get { return lastPlaySeat; }
    }

    public IReadOnlyList<SeatInfo> Seats
    {
        get { return seats; }
    }

    public IReadOnlyList<byte> LastPlayTiles
    {
        get { return lastPlayTiles; }
    }

    public bool CanJoin
    {
        get { return phase == GamePhase.Waiting && seats.Count < GameRules.MaxPlayers; }
    }

    // 클라이언트: 공개 상태가 바뀔 때마다 증가. UI/뷰는 이 값이 바뀔 때만 다시 그림 (매 프레임 문자열 생성 방지)
    public int Version { get; private set; }

    private void Awake()
    {
        seats.OnChange += OnSeatsChanged;
        lastPlayTiles.OnChange += OnLastPlayTilesChanged;
    }

    // 스폰된 오브젝트라 씬 참조를 받을 수 없어서 Mirror의 NetworkManager.singleton으로 매니저를 찾아 등록
    public override void OnStartClient()
    {
        Version++;
        LexioTableNetworkManager manager = NetworkManager.singleton as LexioTableNetworkManager;
        if (manager != null)
        {
            manager.RegisterTable(this);
        }
    }

    public override void OnStopClient()
    {
        LexioTableNetworkManager manager = NetworkManager.singleton as LexioTableNetworkManager;
        if (manager != null)
        {
            manager.UnregisterTable(this);
        }
    }

    private void OnPhaseChanged(GamePhase oldValue, GamePhase newValue)
    {
        Version++;
    }

    private void OnIntChanged(int oldValue, int newValue)
    {
        Version++;
    }

    private void OnBoolChanged(bool oldValue, bool newValue)
    {
        Version++;
    }

    private void OnSeatsChanged(SyncList<SeatInfo>.Operation operation, int index, SeatInfo item)
    {
        Version++;
    }

    private void OnLastPlayTilesChanged(SyncList<byte>.Operation operation, int index, byte item)
    {
        Version++;
    }

    // ---------------- 서버: 입장/퇴장 ----------------

    [Server]
    public void ServerAddPlayer(LexioPlayer player)
    {
        player.Table = this;
        seatPlayers.Add(player);
        SeatInfo info = new SeatInfo();
        info.nickname = "Player" + seatPlayers.Count;
        info.chips = startingChips;
        seats.Add(info);
        player.SetSeat(seatPlayers.Count - 1);

        if (seats.Count >= GameRules.MaxPlayers)
        {
            StartGame();
        }
    }

    [Server]
    public void ServerRemovePlayer(LexioPlayer player)
    {
        int seat = seatPlayers.IndexOf(player);
        if (seat >= 0)
        {
            if (phase == GamePhase.Waiting)
            {
                seatPlayers.RemoveAt(seat);
                seats.RemoveAt(seat);
                for (int i = 0; i < seatPlayers.Count; i++)
                {
                    seatPlayers[i].SetSeat(i);
                }
            }
            else
            {
                // 재접속 없음: 남은 판은 AI가 대신함
                seatPlayers[seat] = null;
                // 게임이 끝난 뒤(결과 화면, 마지막 라운드 정산 대기 포함) 나간 사람은 AI로 바꾸지 않음
                // 자리 정보는 그대로 둬서 남은 사람의 결과 화면에 이름/칩이 그대로 보임
                bool isGameOver = phase == GamePhase.Finished || (game != null && game.IsFinished);
                if (isGameOver == false)
                {
                    SeatInfo info = seats[seat];
                    info.isAi = true;
                    seats[seat] = info;
                }
                turnStartTime = NetworkTime.time;

                if (HasHumanPlayer() == false)
                {
                    ResetToWaiting();
                }
                else
                {
                    // 나간 사람만 준비를 안 한 상태였으면 바로 정리 시간 종료
                    TryEndPreparation();
                }
            }
        }
    }

    [Server]
    public void ServerSetNickname(LexioPlayer player, string nickname)
    {
        int seat = seatPlayers.IndexOf(player);
        if (seat >= 0)
        {
            string trimmed = "";
            if (nickname != null)
            {
                trimmed = nickname.Trim();
            }

            if (trimmed.Length > LexioPlayer.MaxNicknameLength)
            {
                trimmed = trimmed.Substring(0, LexioPlayer.MaxNicknameLength);
            }

            if (trimmed.Length > 0)
            {
                SeatInfo info = seats[seat];
                info.nickname = trimmed;
                seats[seat] = info;
            }
        }
    }

    [Server]
    public void ServerRequestStart(LexioPlayer player)
    {
        if (phase == GamePhase.Waiting)
        {
            if (seats.Count >= GameRules.MinPlayers)
            {
                StartGame();
            }
            else
            {
                player.TargetNotice(GameRules.MinPlayers + "명 이상 모여야 시작할 수 있습니다.");
            }
        }
    }

    // 패 정리 시간에 준비 완료. 사람 전원이 누르면 남은 정리 시간을 건너뜀 (취소 없음)
    [Server]
    public void ServerReady(LexioPlayer player)
    {
        int seat = seatPlayers.IndexOf(player);
        if (seat >= 0 && IsPreparing && seats[seat].isReady == false)
        {
            SeatInfo info = seats[seat];
            info.isReady = true;
            seats[seat] = info;
            TryEndPreparation();
        }
    }

    // AI(나간 자리 포함)는 항상 준비된 것으로 봄
    [Server]
    private void TryEndPreparation()
    {
        if (IsPreparing)
        {
            bool isAllReady = true;
            for (int seat = 0; seat < seatPlayers.Count; seat++)
            {
                if (seatPlayers[seat] != null && seats[seat].isReady == false)
                {
                    isAllReady = false;
                }
            }

            if (isAllReady)
            {
                // 패 돌리는 연출 중에 다 눌렀으면 연출이 끝날 때 시작
                double now = NetworkTime.time;
                if (now < dealEndTime)
                {
                    now = dealEndTime;
                }
                prepareEndTime = now;
                turnStartTime = now;
                turnDeadline = now + turnSeconds;
            }
        }
    }

    // ---------------- 서버: 플레이 ----------------

    [Server]
    public void ServerSubmit(LexioPlayer player, byte[] tileIds)
    {
        int seat = seatPlayers.IndexOf(player);
        if (seat >= 0 && phase == GamePhase.Playing && isWaitingNextRound == false && tileIds != null)
        {
            List<Tile> tiles = new List<Tile>();
            for (int i = 0; i < tileIds.Length; i++)
            {
                tiles.Add(new Tile(tileIds[i]));
            }

            if (IsPreparing)
            {
                player.TargetNotice("패 정리 시간이 끝난 뒤에 낼 수 있습니다.");
            }
            else if (game.TryPlay(seat, tiles))
            {
                AfterPlay(seat, tiles);
            }
            else
            {
                player.TargetNotice("낼 수 없는 패입니다.");
            }
        }
    }

    [Server]
    public void ServerPass(LexioPlayer player)
    {
        int seat = seatPlayers.IndexOf(player);
        if (seat >= 0 && phase == GamePhase.Playing && isWaitingNextRound == false)
        {
            if (game.TryPass(seat))
            {
                AfterPass(seat);
            }
        }
    }

    [ServerCallback]
    private void Update()
    {
        if (phase == GamePhase.Playing && game != null)
        {
            if (isWaitingNextRound)
            {
                if (NetworkTime.time >= nextRoundTime)
                {
                    isWaitingNextRound = false;
                    if (game.IsFinished)
                    {
                        phase = GamePhase.Finished;
                    }
                    else
                    {
                        StartRound();
                    }
                }
            }
            else if (game.CurrentSeat >= 0 && NetworkTime.time >= prepareEndTime)
            {
                bool isAiTurn = seatPlayers[game.CurrentSeat] == null;
                if (isAiTurn)
                {
                    if (NetworkTime.time >= turnStartTime + aiDelaySeconds)
                    {
                        DoAutoAction();
                    }
                }
                else if (NetworkTime.time >= turnDeadline)
                {
                    DoAutoAction();
                }
            }
        }
    }

    // 시간 초과 / AI: 리드 차례면 가장 약한 싱글(라운드 첫 선이면 구름3), 아니면 패스
    [Server]
    private void DoAutoAction()
    {
        int seat = game.CurrentSeat;
        if (game.IsLeadTurn)
        {
            List<Tile> tiles = new List<Tile>();
            tiles.Add(game.GetWeakestTile(seat));
            if (game.TryPlay(seat, tiles))
            {
                AfterPlay(seat, tiles);
            }
        }
        else
        {
            if (game.TryPass(seat))
            {
                AfterPass(seat);
            }
        }
    }

    [Server]
    private void StartGame()
    {
        game = new LexioGame(seats.Count, roundCount, startingChips, random);
        maxNumber = game.MaxNumber;
        phase = GamePhase.Playing;
        StartRound();
    }

    [Server]
    private void StartRound()
    {
        game.StartRound();
        roundNumber = game.RoundNumber;
        lastPlayTiles.Clear();
        lastPlaySeat = -1;

        for (int seat = 0; seat < seats.Count; seat++)
        {
            SeatInfo info = seats[seat];
            info.isReady = false;
            seats[seat] = info;
        }

        for (int seat = 0; seat < seatPlayers.Count; seat++)
        {
            SendHand(seat, true);
        }
        AfterTurnChanged();

        // 패 돌리는 동안은 턴 시간/AI 대기 시간을 세지 않음
        dealEndTime = NetworkTime.time + dealSeconds;
        // 패를 다 받은 뒤 패 정리 시간까지 지나야 첫 턴 시작
        prepareEndTime = dealEndTime + prepareSeconds;
        turnStartTime = prepareEndTime;
        turnDeadline = prepareEndTime + turnSeconds;
    }

    [Server]
    private void AfterPlay(int seat, List<Tile> tiles)
    {
        byte[] tileIds = new byte[tiles.Count];
        lastPlayTiles.Clear();
        for (int i = 0; i < tiles.Count; i++)
        {
            lastPlayTiles.Add(tiles[i].Id);
            tileIds[i] = tiles[i].Id;
        }
        lastPlaySeat = seat;
        SendHand(seat, false);
        RpcShowPlay(seat, tileIds);

        if (game.IsRoundOver)
        {
            isWaitingNextRound = true;
            nextRoundTime = NetworkTime.time + roundEndDelaySeconds;
            RevealHands();
        }
        AfterTurnChanged();
    }

    // 라운드 결과 공개: 모든 자리의 남은 패를 한 번에 보냄 (라운드가 끝났으므로 공개해도 됨)
    // 자리 순서로 이어 붙인 타일 ID와 자리별 장수를 함께 보냄
    [Server]
    private void RevealHands()
    {
        int[] counts = new int[seats.Count];
        int total = 0;
        for (int seat = 0; seat < seats.Count; seat++)
        {
            counts[seat] = game.GetHand(seat).Count;
            total += counts[seat];
        }

        byte[] tileIds = new byte[total];
        int index = 0;
        for (int seat = 0; seat < seats.Count; seat++)
        {
            IReadOnlyList<Tile> hand = game.GetHand(seat);
            for (int i = 0; i < hand.Count; i++)
            {
                tileIds[index] = hand[i].Id;
                index++;
            }
        }
        RpcRevealHands(tileIds, counts);
    }

    [Server]
    private void AfterPass(int seat)
    {
        RpcShowPass(seat);
        AfterTurnChanged();
    }

    [Server]
    private void AfterTurnChanged()
    {
        if (game.WasTrickCleared)
        {
            lastPlayTiles.Clear();
            lastPlaySeat = -1;
        }

        for (int seat = 0; seat < seats.Count; seat++)
        {
            SeatInfo info = seats[seat];
            info.chips = game.GetChips(seat);
            info.tileCount = game.GetHand(seat).Count;
            // 정산 기준 장수는 칩과 같은 동기화에 실려야 클라이언트가 주고받는 순서를 계산할 수 있음
            info.settleCount = 0;
            if (game.LastEffectiveCounts != null)
            {
                info.settleCount = game.LastEffectiveCounts[seat];
            }
            seats[seat] = info;
        }

        currentSeat = game.CurrentSeat;
        isLeadTurn = game.IsLeadTurn;
        turnStartTime = NetworkTime.time;
        turnDeadline = NetworkTime.time + turnSeconds;
    }

    [Server]
    private void SendHand(int seat, bool isNewDeal)
    {
        LexioPlayer player = seatPlayers[seat];
        if (player != null)
        {
            IReadOnlyList<Tile> hand = game.GetHand(seat);
            byte[] tileIds = new byte[hand.Count];
            for (int i = 0; i < hand.Count; i++)
            {
                tileIds[i] = hand[i].Id;
            }
            player.TargetSetHand(tileIds, isNewDeal, seat, seats.Count);
        }
    }

    // 말풍선용: 누가 무엇을 냈는지 / 패스했는지 모든 클라이언트에 알림
    // 낸 패는 lastPlayTiles로도 동기화되지만, 트릭이 끝나면 바로 비워지므로 따로 보냄
    [ClientRpc]
    private void RpcShowPlay(int seat, byte[] tileIds)
    {
        LexioTableNetworkManager manager = NetworkManager.singleton as LexioTableNetworkManager;
        if (manager != null)
        {
            manager.RaisePlayReceived(seat, tileIds);
        }
    }

    [ClientRpc]
    private void RpcShowPass(int seat)
    {
        LexioTableNetworkManager manager = NetworkManager.singleton as LexioTableNetworkManager;
        if (manager != null)
        {
            manager.RaisePassReceived(seat);
        }
    }

    [ClientRpc]
    private void RpcRevealHands(byte[] tileIds, int[] counts)
    {
        LexioTableNetworkManager manager = NetworkManager.singleton as LexioTableNetworkManager;
        if (manager != null)
        {
            manager.RaiseHandsRevealed(tileIds, counts);
        }
    }

    [Server]
    private bool HasHumanPlayer()
    {
        bool result = false;
        for (int i = 0; i < seatPlayers.Count; i++)
        {
            if (seatPlayers[i] != null)
            {
                result = true;
            }
        }
        return result;
    }

    [Server]
    private void ResetToWaiting()
    {
        game = null;
        isWaitingNextRound = false;
        seatPlayers.Clear();
        seats.Clear();
        lastPlayTiles.Clear();
        lastPlaySeat = -1;
        currentSeat = -1;
        isLeadTurn = false;
        roundNumber = 0;
        maxNumber = 0;
        phase = GamePhase.Waiting;
    }
}
