using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

// 접속 → 대기실 → 게임 → 최종 순위 화면 전환과 버튼 처리
// 텍스트는 테이블 상태(Version)나 선택이 바뀔 때만 다시 만들어서 매 프레임 문자열 할당을 피함
public class LexioUI : MonoBehaviour
{
    private const int CachedSecondCount = 100;

    // 제출/패스를 지금 할 수 없는 이유 (None = 가능)
    private enum BlockReason
    {
        None,
        NotPlaying,
        Dealing,
        Preparing,
        NotMyTurn,
        NoSelection,
        NotCombination,
        CountMismatch,
        TooWeak,
        LeadCannotPass
    }

    [Header("References")]
    [SerializeField] private LexioTableNetworkManager networkManager;
    [SerializeField] private LexioClientView clientView;
    // 정산 중 칩 숫자를 칩이 오가는 대로 1씩 바꿔 보여주기 위해 사용
    [SerializeField] private ChipFlightView chipView;

    [Header("Connect")]
    [SerializeField] private GameObject connectPanel;
    [SerializeField] private InputField nicknameInput;
    [SerializeField] private Button connectButton;
    [SerializeField] private Text connectStatusText;

    [Header("Lobby")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private Text lobbyPlayersText;
    [SerializeField] private Button startButton;

    [Header("Game")]
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button passButton;
    [SerializeField] private Button sortSuitButton;
    [SerializeField] private Button sortNumberButton;
    // 패 정리 시간에만 보임. 사람 전원이 누르면 정리 시간이 바로 끝남
    [SerializeField] private Button readyButton;
    [SerializeField] private Text timerText;
    [SerializeField] private Text roundText;
    [SerializeField] private Text turnText;
    [SerializeField] private Text myInfoText;
    [SerializeField] private Text selectionText;
    [SerializeField] private Text lastPlayText;
    [SerializeField] private RectTransform[] seatLabels;
    [SerializeField] private Text[] seatLabelTexts;

    [Header("Result")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Text resultText;
    [SerializeField] private Button exitButton;

    [Header("Common")]
    [SerializeField] private Text noticeText;
    [SerializeField] private float noticeSeconds = 3f;
    [SerializeField] private Color normalLabelColor = Color.white;
    [SerializeField] private Color turnLabelColor = new Color(1f, 0.85f, 0.2f, 1f);
    // 선택한 조합을 지금 바로 낼 수 있을 때 족보 이름 색
    [SerializeField] private Color playableSelectionColor = new Color(1f, 0.85f, 0.2f, 1f);
    // 제출/패스를 지금 할 수 없을 때 버튼 투명도 (누르는 건 가능)
    [SerializeField] private float unavailableButtonAlpha = 0.5f;

    private readonly string[] secondTexts = new string[CachedSecondCount];

    private readonly StringBuilder builder = new StringBuilder(256);
    private readonly List<Tile> selectedTiles = new List<Tile>();
    private readonly List<Tile> lastPlayTiles = new List<Tile>();
    private readonly List<int> resultOrder = new List<int>();
    private int[] resultChips = new int[0];
    private int[] resultRanks = new int[0];
    private readonly List<int> settleCounts = new List<int>();
    private int[] settleChanges = new int[0];
    private bool isSettling;
    private Combination lastPlayCombination;
    private CanvasGroup submitGroup;
    private CanvasGroup passGroup;
    private Color defaultSelectionColor;
    private bool isStartRequested;
    private bool isReadyRequested;
    private bool isExitRequested;
    private bool hasJoined;
    private bool isSelectionDirty = true;
    private bool wasPreparing;
    private int cachedVersion = -1;
    private int cachedSeat = -1;
    private int cachedChipVersion = -1;
    private int shownSeconds = -1;
    private float noticeHideTime;

    private void OnEnable()
    {
        networkManager.NoticeReceived += OnNoticeReceived;
        networkManager.ClientDisconnected += OnClientDisconnected;
        clientView.SelectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        networkManager.NoticeReceived -= OnNoticeReceived;
        networkManager.ClientDisconnected -= OnClientDisconnected;
        clientView.SelectionChanged -= OnSelectionChanged;
    }

    private void Start()
    {
        connectButton.onClick.AddListener(OnConnectClicked);
        startButton.onClick.AddListener(OnStartClicked);
        submitButton.onClick.AddListener(OnSubmitClicked);
        passButton.onClick.AddListener(OnPassClicked);
        sortSuitButton.onClick.AddListener(OnSortSuitClicked);
        sortNumberButton.onClick.AddListener(OnSortNumberClicked);
        readyButton.onClick.AddListener(OnReadyClicked);
        exitButton.onClick.AddListener(OnExitClicked);
        nicknameInput.characterLimit = LexioPlayer.MaxNicknameLength;
        submitGroup = GetOrAddCanvasGroup(submitButton);
        passGroup = GetOrAddCanvasGroup(passButton);
        defaultSelectionColor = selectionText.color;
        connectStatusText.text = "";
        noticeText.text = "";
    }

    private void Update()
    {
        LexioTable table = networkManager.Table;
        LexioPlayer local = networkManager.LocalPlayer;
        bool isReady = NetworkClient.isConnected && table != null && local != null;

        if (isReady)
        {
            hasJoined = true;
        }

        bool showConnect = isReady == false;
        bool showLobby = isReady && table.Phase == GamePhase.Waiting;
        bool showGame = isReady && table.Phase != GamePhase.Waiting;
        bool showResult = isReady && table.Phase == GamePhase.Finished;

        SetActive(connectPanel, showConnect);
        SetActive(lobbyPanel, showLobby);
        SetActive(gamePanel, showGame);
        SetActive(resultPanel, showResult);

        if (showConnect)
        {
            connectButton.interactable = NetworkClient.active == false;
        }

        if (showLobby)
        {
            startButton.interactable = isStartRequested == false;
        }
        else
        {
            isStartRequested = false;
        }

        if (isReady)
        {
            // 패 정리 시간은 시간이 지나서 끝나므로(동기화 변경 없음) 끝나는 순간 화면을 다시 그림
            bool isPreparing = table.IsPreparing;
            if (isPreparing != wasPreparing)
            {
                wasPreparing = isPreparing;
                cachedVersion = -1;
            }

            if (table.Version != cachedVersion || local.Seat != cachedSeat || chipView.ChipVersion != cachedChipVersion)
            {
                cachedVersion = table.Version;
                cachedSeat = local.Seat;
                cachedChipVersion = chipView.ChipVersion;
                isSelectionDirty = true;
                RefreshState(table, local.Seat, showLobby, showGame, showResult);
            }

            if (showGame)
            {
                if (isSelectionDirty)
                {
                    isSelectionDirty = false;
                    RefreshSelection(table, local.Seat);
                }
                UpdateTimer(table);
                UpdateSeatLabelPositions(table, local.Seat);
            }
            else
            {
                HideSeatLabels();
            }
        }
        else
        {
            cachedVersion = -1;
            HideSeatLabels();
        }

        if (noticeText.text.Length > 0 && Time.time >= noticeHideTime)
        {
            noticeText.text = "";
        }
    }

    // 테이블 상태가 바뀐 프레임에만 호출
    private void RefreshState(LexioTable table, int mySeat, bool showLobby, bool showGame, bool showResult)
    {
        if (showLobby)
        {
            RefreshLobby(table);
        }

        if (showGame)
        {
            RefreshGame(table, mySeat);
        }

        if (showResult)
        {
            RefreshResult(table);
        }
    }

    // ---------------- 접속 ----------------

    private void OnConnectClicked()
    {
        string nickname = nicknameInput.text.Trim();
        if (nickname.Length > 0)
        {
            networkManager.PendingNickname = nickname;
            isExitRequested = false;
            hasJoined = false;
            connectStatusText.text = "접속 중... (" + networkManager.networkAddress + ")";
            networkManager.StartClient();
        }
        else
        {
            connectStatusText.text = "닉네임을 입력하세요.";
        }
    }

    private void OnClientDisconnected()
    {
        if (isExitRequested)
        {
            connectStatusText.text = "";
        }
        else if (hasJoined)
        {
            connectStatusText.text = "서버와 연결이 끊어졌습니다.";
        }
        else
        {
            connectStatusText.text = "접속할 수 없습니다. (서버 꺼짐 / 게임 진행 중 / 인원 초과)";
        }
        hasJoined = false;
        isStartRequested = false;
        cachedVersion = -1;
    }

    // ---------------- 대기실 ----------------

    private void RefreshLobby(LexioTable table)
    {
        builder.Clear();
        builder.Append("대기 중 (").Append(table.Seats.Count).Append(" / ").Append(GameRules.MaxPlayers).Append(")\n\n");
        for (int i = 0; i < table.Seats.Count; i++)
        {
            builder.Append(i + 1).Append(". ").Append(table.Seats[i].nickname).Append('\n');
        }
        lobbyPlayersText.text = builder.ToString();
    }

    private void OnStartClicked()
    {
        LexioPlayer local = networkManager.LocalPlayer;
        if (local != null)
        {
            isStartRequested = true;
            local.CmdRequestStart();
        }
    }

    // ---------------- 게임 ----------------

    private void RefreshGame(LexioTable table, int mySeat)
    {
        roundText.text = "라운드 " + table.RoundNumber + " / " + table.RoundCount;

        if (table.IsPreparing)
        {
            // AI(나간 자리)는 서버가 준비된 것으로 보므로 사람만 셈
            int humanCount = 0;
            int readyCount = 0;
            for (int i = 0; i < table.Seats.Count; i++)
            {
                if (table.Seats[i].isAi == false)
                {
                    humanCount++;
                    if (table.Seats[i].isReady)
                    {
                        readyCount++;
                    }
                }
            }
            builder.Clear();
            builder.Append("패 정리 시간 (준비 ").Append(readyCount).Append(" / ").Append(humanCount).Append(')');
            turnText.text = builder.ToString();
        }
        else if (table.Phase == GamePhase.Playing && table.CurrentSeat >= 0)
        {
            builder.Clear();
            if (table.CurrentSeat == mySeat)
            {
                builder.Append("내 차례");
            }
            else
            {
                builder.Append(GetNickname(table, table.CurrentSeat)).Append(" 차례");
            }

            if (table.IsLeadTurn)
            {
                builder.Append(" (선)");
            }
            turnText.text = builder.ToString();
        }
        else
        {
            turnText.text = "";
        }

        RefreshSettle(table);

        if (mySeat >= 0 && mySeat < table.Seats.Count)
        {
            SeatInfo mine = table.Seats[mySeat];
            builder.Clear();
            builder.Append(mine.nickname).Append("\n칩 ").Append(chipView.GetShownChips(mySeat, mine.chips)).Append("\n남은 패 ").Append(mine.tileCount).Append("장");
            if (isSettling)
            {
                builder.Append("\n정산 ");
                AppendSettleText(mine, settleChanges[mySeat]);
            }
            myInfoText.text = builder.ToString();
        }

        // 마지막 낸 패: 상태가 바뀔 때 한 번만 판정해서 보관 (제출 가능 여부 계산에도 재사용)
        lastPlayCombination = null;
        lastPlayTiles.Clear();
        for (int i = 0; i < table.LastPlayTiles.Count; i++)
        {
            lastPlayTiles.Add(new Tile(table.LastPlayTiles[i]));
        }
        if (lastPlayTiles.Count > 0)
        {
            lastPlayCombination = CombinationEvaluator.Evaluate(lastPlayTiles, table.MaxNumber);
        }

        if (lastPlayCombination != null && table.LastPlaySeat >= 0)
        {
            lastPlayText.text = GetNickname(table, table.LastPlaySeat) + " : " + lastPlayCombination.DisplayName;
        }
        else
        {
            lastPlayText.text = "";
        }

        RefreshSeatLabelTexts(table, mySeat);
    }

    // 선택이 바뀌었거나 테이블 상태가 바뀐 프레임에만 호출
    private void RefreshSelection(LexioTable table, int mySeat)
    {
        bool isDealing = clientView.IsDealing;
        Combination combination = EvaluateSelection(table);
        bool canSubmit = GetSubmitBlockReason(table, mySeat, combination) == BlockReason.None;

        if (selectedTiles.Count == 0)
        {
            selectionText.text = "";
        }
        else if (combination == null)
        {
            selectionText.text = "족보가 아닙니다";
        }
        else
        {
            selectionText.text = combination.DisplayName;
        }
        // 지금 바로 낼 수 있는 조합이면 강조색
        if (canSubmit)
        {
            selectionText.color = playableSelectionColor;
        }
        else
        {
            selectionText.color = defaultSelectionColor;
        }

        // 제출/패스는 항상 누를 수 있고(누르면 못 내는 이유를 알려줌), 지금 안 되는 상태면 흐리게만 표시
        submitGroup.alpha = GetButtonAlpha(canSubmit);
        passGroup.alpha = GetButtonAlpha(GetPassBlockReason(table, mySeat) == BlockReason.None);
        sortSuitButton.interactable = isDealing == false;
        sortNumberButton.interactable = isDealing == false;

        bool isPreparing = table.IsPreparing;
        if (isPreparing == false)
        {
            isReadyRequested = false;
        }
        bool isMeReady = mySeat >= 0 && mySeat < table.Seats.Count && table.Seats[mySeat].isReady;
        SetActive(readyButton.gameObject, isPreparing);
        // 준비 완료 버튼이 제출 버튼 자리를 같이 쓰므로, 패 정리 시간에는 제출 버튼을 숨김
        SetActive(submitButton.gameObject, isPreparing == false);
        readyButton.interactable = isPreparing && isDealing == false && isMeReady == false && isReadyRequested == false;
    }

    // 남은 초가 바뀔 때만 텍스트 교체 (숫자 문자열은 캐시)
    private void UpdateTimer(LexioTable table)
    {
        int seconds = -1;
        // 패 돌리는 중에는 표시 안 함 (서버 마감 시간에 연출 시간이 더해져 있음)
        if (table.Phase == GamePhase.Playing && table.CurrentSeat >= 0 && clientView.IsDealing == false)
        {
            double endTime = table.TurnDeadline;
            if (table.IsPreparing)
            {
                endTime = table.PrepareEndTime;
            }
            double remaining = endTime - NetworkTime.time;
            if (remaining < 0)
            {
                remaining = 0;
            }
            seconds = Mathf.CeilToInt((float)remaining);
        }

        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            timerText.text = GetSecondText(seconds);
        }
    }

    private void RefreshSeatLabelTexts(LexioTable table, int mySeat)
    {
        for (int i = 0; i < seatLabelTexts.Length; i++)
        {
            if (i < table.Seats.Count && i != mySeat)
            {
                SeatInfo info = table.Seats[i];
                builder.Clear();
                builder.Append(info.nickname);
                if (info.isAi)
                {
                    builder.Append(" (AI)");
                }
                builder.Append("\n칩 ").Append(chipView.GetShownChips(i, info.chips)).Append(" · ").Append(info.tileCount).Append("장");
                if (table.IsPreparing && info.isReady)
                {
                    builder.Append(" · 준비");
                }
                if (isSettling)
                {
                    builder.Append('\n');
                    AppendSettleText(info, settleChanges[i]);
                }
                seatLabelTexts[i].text = builder.ToString();

                if (table.Phase == GamePhase.Playing && table.CurrentSeat == i)
                {
                    seatLabelTexts[i].color = turnLabelColor;
                }
                else
                {
                    seatLabelTexts[i].color = normalLabelColor;
                }
            }
        }
    }

    // 라운드 정산 중인지와 자리별 칩 변화. 정산 장수는 라운드가 끝났을 때만 0이 아님 (다음 라운드 시작 때 0으로 돌아감)
    // 칩 변화는 서버와 같은 계산(ScoreCalculator)을 정산 장수로 다시 해서 얻음
    private void RefreshSettle(LexioTable table)
    {
        isSettling = false;
        settleCounts.Clear();
        for (int i = 0; i < table.Seats.Count; i++)
        {
            settleCounts.Add(table.Seats[i].settleCount);
            if (table.Seats[i].settleCount > 0)
            {
                isSettling = true;
            }
        }

        if (isSettling)
        {
            settleChanges = ScoreCalculator.CalculateChipChanges(settleCounts);
        }
    }

    // "4장 × 2 = 8 · 칩 -5" (2가 없으면 "4장 · 칩 -5")
    private void AppendSettleText(SeatInfo info, int change)
    {
        builder.Append(info.tileCount).Append("장");
        if (info.tileCount > 0 && info.settleCount > info.tileCount)
        {
            builder.Append(" × ").Append(info.settleCount / info.tileCount).Append(" = ").Append(info.settleCount);
        }
        builder.Append(" · 칩 ");
        if (change > 0)
        {
            builder.Append('+');
        }
        builder.Append(change);
    }

    // 위치 계산만 매 프레임 (할당 없음)
    private void UpdateSeatLabelPositions(LexioTable table, int mySeat)
    {
        Camera viewCamera = clientView.ViewCamera;
        for (int i = 0; i < seatLabels.Length; i++)
        {
            bool isVisible = false;
            if (i < table.Seats.Count && i != mySeat && viewCamera != null)
            {
                if (clientView.TryGetSeatAnchor(i, out Vector3 anchor))
                {
                    Vector3 screenPoint = viewCamera.WorldToScreenPoint(anchor);
                    if (screenPoint.z > 0f)
                    {
                        isVisible = true;
                        seatLabels[i].position = new Vector3(screenPoint.x, screenPoint.y, 0f);
                    }
                }
            }
            SetActive(seatLabels[i].gameObject, isVisible);
        }
    }

    private void HideSeatLabels()
    {
        for (int i = 0; i < seatLabels.Length; i++)
        {
            SetActive(seatLabels[i].gameObject, false);
        }
    }

    // 못 내는 경우 서버에 보내지 않고 이유만 알림 (최종 판정은 서버가 다시 함)
    private void OnSubmitClicked()
    {
        LexioPlayer local = networkManager.LocalPlayer;
        LexioTable table = networkManager.Table;
        if (local != null && table != null)
        {
            Combination combination = EvaluateSelection(table);
            BlockReason reason = GetSubmitBlockReason(table, local.Seat, combination);
            if (reason == BlockReason.None)
            {
                byte[] tileIds = new byte[selectedTiles.Count];
                for (int i = 0; i < selectedTiles.Count; i++)
                {
                    tileIds[i] = selectedTiles[i].Id;
                }
                local.CmdSubmit(tileIds);
                clientView.ClearSelection();
            }
            else
            {
                ShowNotice(GetBlockMessage(reason));
            }
        }
    }

    private void OnPassClicked()
    {
        LexioPlayer local = networkManager.LocalPlayer;
        LexioTable table = networkManager.Table;
        if (local != null && table != null)
        {
            BlockReason reason = GetPassBlockReason(table, local.Seat);
            if (reason == BlockReason.None)
            {
                local.CmdPass();
            }
            else
            {
                ShowNotice(GetBlockMessage(reason));
            }
        }
    }

    // 선택한 타일을 selectedTiles에 채우고 족보 판정 (족보가 아니거나 선택이 없으면 null)
    private Combination EvaluateSelection(LexioTable table)
    {
        clientView.GetSelectedTiles(selectedTiles);
        Combination combination = null;
        if (selectedTiles.Count > 0)
        {
            combination = CombinationEvaluator.Evaluate(selectedTiles, table.MaxNumber);
        }
        return combination;
    }

    // 제출/패스 공통: 지금 내 차례에 행동할 수 있는 상태인지
    private BlockReason GetTurnBlockReason(LexioTable table, int mySeat)
    {
        BlockReason reason = BlockReason.None;
        if (table.Phase != GamePhase.Playing || table.CurrentSeat < 0)
        {
            reason = BlockReason.NotPlaying;
        }
        else if (clientView.IsDealing)
        {
            reason = BlockReason.Dealing;
        }
        else if (table.IsPreparing)
        {
            reason = BlockReason.Preparing;
        }
        else if (table.CurrentSeat != mySeat)
        {
            reason = BlockReason.NotMyTurn;
        }
        return reason;
    }

    // 선택 상태는 EvaluateSelection을 먼저 호출해서 selectedTiles/combination이 최신이어야 함
    private BlockReason GetSubmitBlockReason(LexioTable table, int mySeat, Combination combination)
    {
        BlockReason reason = GetTurnBlockReason(table, mySeat);
        if (reason == BlockReason.None)
        {
            Combination previous = null;
            if (table.IsLeadTurn == false)
            {
                previous = lastPlayCombination;
            }

            if (selectedTiles.Count == 0)
            {
                reason = BlockReason.NoSelection;
            }
            else if (combination == null)
            {
                reason = BlockReason.NotCombination;
            }
            else if (previous != null && previous.Count != combination.Count)
            {
                reason = BlockReason.CountMismatch;
            }
            else if (CombinationEvaluator.CanBeat(previous, combination) == false)
            {
                reason = BlockReason.TooWeak;
            }
        }
        return reason;
    }

    private BlockReason GetPassBlockReason(LexioTable table, int mySeat)
    {
        BlockReason reason = GetTurnBlockReason(table, mySeat);
        if (reason == BlockReason.None && table.IsLeadTurn)
        {
            reason = BlockReason.LeadCannotPass;
        }
        return reason;
    }

    // 버튼을 누른 순간에만 호출 (장수 안내 문자열은 이때만 만듦)
    private string GetBlockMessage(BlockReason reason)
    {
        string message;
        switch (reason)
        {
            case BlockReason.NotPlaying:
                message = "지금은 낼 수 없습니다.";
                break;
            case BlockReason.Dealing:
                message = "아직 패를 돌리는 중입니다.";
                break;
            case BlockReason.Preparing:
                message = "패 정리 시간이 끝난 뒤에 할 수 있습니다.";
                break;
            case BlockReason.NotMyTurn:
                message = "내 차례가 아닙니다.";
                break;
            case BlockReason.NoSelection:
                message = "낼 타일을 선택하세요.";
                break;
            case BlockReason.NotCombination:
                message = "족보가 아닙니다.";
                break;
            case BlockReason.CountMismatch:
                message = "앞 패와 장수가 같아야 합니다. (지금은 " + lastPlayCombination.Count + "장)";
                break;
            case BlockReason.TooWeak:
                message = "앞 패보다 약합니다.";
                break;
            case BlockReason.LeadCannotPass:
                message = "선은 패스할 수 없습니다.";
                break;
            default:
                message = "";
                break;
        }
        return message;
    }

    private float GetButtonAlpha(bool isAvailable)
    {
        float alpha = unavailableButtonAlpha;
        if (isAvailable)
        {
            alpha = 1f;
        }
        return alpha;
    }

    // 프리팹/씬을 다시 만들지 않아도 되도록 실행 중에 붙임
    private CanvasGroup GetOrAddCanvasGroup(Button button)
    {
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = button.gameObject.AddComponent<CanvasGroup>();
        }
        return group;
    }

    private void OnReadyClicked()
    {
        LexioPlayer local = networkManager.LocalPlayer;
        if (local != null)
        {
            // 서버 반영 전에 다시 누르지 않도록 바로 비활성화
            isReadyRequested = true;
            readyButton.interactable = false;
            local.CmdReady();
        }
    }

    private void OnSortSuitClicked()
    {
        clientView.SortHand(HandSortMode.SuitFirst);
    }

    private void OnSortNumberClicked()
    {
        clientView.SortHand(HandSortMode.NumberFirst);
    }

    private void OnSelectionChanged()
    {
        isSelectionDirty = true;
    }

    // ---------------- 최종 결과 ----------------

    private void RefreshResult(LexioTable table)
    {
        int count = table.Seats.Count;
        if (resultChips.Length != count)
        {
            resultChips = new int[count];
        }
        for (int i = 0; i < count; i++)
        {
            resultChips[i] = table.Seats[i].chips;
        }
        resultRanks = ScoreCalculator.CalculateRanks(resultChips);

        // 순위 오름차순 정렬 (인원이 최대 5명이라 단순 삽입 정렬)
        resultOrder.Clear();
        for (int i = 0; i < count; i++)
        {
            int insertIndex = resultOrder.Count;
            for (int j = 0; j < resultOrder.Count; j++)
            {
                if (insertIndex == resultOrder.Count && resultRanks[i] < resultRanks[resultOrder[j]])
                {
                    insertIndex = j;
                }
            }
            resultOrder.Insert(insertIndex, i);
        }

        builder.Clear();
        builder.Append("최종 순위\n\n");
        for (int i = 0; i < resultOrder.Count; i++)
        {
            int seat = resultOrder[i];
            builder.Append(resultRanks[seat]).Append("위   ").Append(table.Seats[seat].nickname);
            if (table.Seats[seat].isAi)
            {
                builder.Append(" (AI)");
            }
            builder.Append("   칩 ").Append(resultChips[seat]).Append('\n');
        }
        resultText.text = builder.ToString();
    }

    private void OnExitClicked()
    {
        isExitRequested = true;
        networkManager.StopClient();
    }

    // ---------------- 공통 ----------------

    private void OnNoticeReceived(string message)
    {
        isStartRequested = false;
        ShowNotice(message);
    }

    private void ShowNotice(string message)
    {
        noticeText.text = message;
        noticeHideTime = Time.time + noticeSeconds;
    }

    private string GetSecondText(int seconds)
    {
        string text = "";
        if (seconds >= 0 && seconds < CachedSecondCount)
        {
            if (secondTexts[seconds] == null)
            {
                secondTexts[seconds] = seconds.ToString();
            }
            text = secondTexts[seconds];
        }
        else if (seconds >= CachedSecondCount)
        {
            text = seconds.ToString();
        }
        return text;
    }

    private string GetNickname(LexioTable table, int seat)
    {
        string nickname = "";
        if (seat >= 0 && seat < table.Seats.Count)
        {
            nickname = table.Seats[seat].nickname;
        }
        return nickname;
    }

    private void SetActive(GameObject target, bool isActive)
    {
        if (target.activeSelf != isActive)
        {
            target.SetActive(isActive);
        }
    }
}
