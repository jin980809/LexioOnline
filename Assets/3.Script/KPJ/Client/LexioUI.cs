using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
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
    [SerializeField] private LexioTableView clientView;
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
    // 선택한 패를 모두 해제 (항상 누를 수 있음)
    [SerializeField] private Button clearSelectionButton;
    [SerializeField] private Button sortSuitButton;
    [SerializeField] private Button sortNumberButton;
    // 패 정리 시간에만 보임. 사람 전원이 누르면 정리 시간이 바로 끝남
    [SerializeField] private Button readyButton;
    [SerializeField] private Text timerText;
    // 남은 시간 비율 (1 = 방금 시작, 0 = 마감). 씬에 없으면 건너뜀
    [SerializeField] private Slider timerSlider;
    // 내 차례에 남은 시간이 이 초 이하면 경고: 게이지 이미지를 timerWarningSprite로 바꾸고 타이머 전체(timerRoot)를 좌우로 흔듦
    [SerializeField] private int timerWarningSeconds = 5;
    // 비워 두면 게이지 이미지는 바꾸지 않음
    [SerializeField] private Sprite timerWarningSprite;
    // 흔들 타이머 오브젝트 (게이지 + 숫자의 부모). 비워 두면 흔들지 않음
    [SerializeField] private RectTransform timerRoot;
    // 흔들리는 최대 각도(도)와 빠르기 (클수록 빠름)
    [SerializeField] private float timerShakeAngle = 8f;
    [SerializeField] private float timerShakeSpeed = 25f;
    [SerializeField] private Text roundText;
    [SerializeField] private Text turnText;
    // 차례 글자 칸은 배너 이미지보다 넓으므로, 긴 닉네임은 배너 너비에서 양쪽 여백을 뺀 너비에 맞춤
    [SerializeField] private float turnTextPadding = 20f;
    // 패 정리 시간에만 켜지는 준비 인원 패널 ("준비 2 / 4"). 씬에 없으면 건너뜀
    [SerializeField] private GameObject readyPanel;
    [SerializeField] private Text readyCountText;
    [SerializeField] private Text selectionText;
    [SerializeField] private Text lastPlayText;

    [Header("My Info (내 정보, 씬에 없는 칸은 건너뜀)")]
    [FormerlySerializedAs("myInfoText")]
    [SerializeField] private Text myNameText;
    [SerializeField] private Text myChipText;
    [SerializeField] private Text myTileText;
    [SerializeField] private Image myProfileImage;
    // 패 정리 시간에 내가 준비 완료하면 켜짐 (상대 이름표의 ReadyIcon과 같음)
    [SerializeField] private GameObject myReadyIcon;
    // 내 정보 배경. 내 차례 동안 myTurnColor로 물들고 원래 색 쪽으로 옅어졌다 돌아오기를 반복
    [SerializeField] private Image myInfoPanel;
    [SerializeField] private Color myTurnColor = new Color(1f, 0.85f, 0.4f, 1f);
    // 깜빡이는 빠르기 (클수록 빠름)
    [SerializeField] private float myTurnPulseSpeed = 4f;

    [Header("Seat Labels (상대 이름표, 자리 번호 순서)")]
    [SerializeField] private RectTransform[] seatLabels;
    [FormerlySerializedAs("seatLabelTexts")]
    [SerializeField] private Text[] seatNameTexts;
    // 아래 배열은 비워 두거나 칸이 None이면 그 칸을 건너뜀
    [SerializeField] private Text[] seatChipTexts;
    [SerializeField] private Text[] seatTileTexts;
    [SerializeField] private Image[] seatProfileImages;
    // 패 정리 시간에 준비 완료한 상대만 켜짐
    [SerializeField] private GameObject[] seatReadyIcons;
    // 이름표 배경(seatLabels의 Image). 차례인 사람만 turnLabelSprite로 바뀜. 둘 다 비어 있으면 바꾸지 않음
    [SerializeField] private Sprite normalLabelSprite;
    [SerializeField] private Sprite turnLabelSprite;
    // 이름표와 그 위 말풍선이 가리면 안 되는 UI (차례 배너, 준비 인원 패널). 겹치면 이름표를 겹치지 않을 만큼만 옆으로 옮김
    // 꺼져 있어도 피함 (준비 패널이 켜지고 꺼질 때 이름표가 들썩이지 않도록)
    [SerializeField] private RectTransform[] labelAvoidRects;
    // 이름표 위에 뜨는 말풍선 자리 (캔버스 단위). 겹침 판정 때 이름표 위로 이만큼 더 있다고 봄
    [SerializeField] private float labelBubbleReserve = 100f;
    // 말풍선 자리의 너비 (캔버스 단위). 말풍선은 글자 길이에 따라 너비가 바뀌므로 긴 문구("스트레이트 플러시 해 15") 기준으로 잡음
    [SerializeField] private float labelBubbleReserveWidth = 440f;
    // 비켜 놓을 때 띄우는 간격 (캔버스 단위)
    [SerializeField] private float labelAvoidGap = 16f;

    [Header("Profile")]
    // 인덱스 = 유저 DB의 imageNum (SeatInfo.profileImage). 범위를 넘으면 0번 사용
    [SerializeField] private Sprite[] profileSprites;

    [Header("Settle (정산 계산식은 말풍선에 표시)")]
    [SerializeField] private ActionBubbleView bubbleView;
    [SerializeField] private Color chipGainColor = new Color(0.2f, 0.6f, 0.25f, 1f);
    [SerializeField] private Color chipLossColor = new Color(0.85f, 0.25f, 0.2f, 1f);

    [Header("Result")]
    [SerializeField] private GameObject resultPanel;
    // 순위 줄을 그리는 결과 창. 비어 있으면 resultText에 글자로만 표시
    [SerializeField] private ResultPanelView resultView;
    [SerializeField] private Text resultText;
    [SerializeField] private Button exitButton;

    [Header("Common")]
    [SerializeField] private Text noticeText;
    [SerializeField] private float noticeSeconds = 3f;
    // 선택한 조합을 지금 바로 낼 수 있을 때 족보 이름 색
    [SerializeField] private Color playableSelectionColor = new Color(1f, 0.85f, 0.2f, 1f);
    // 최종 결과에서 내 줄 글자색
    [SerializeField] private Color myResultColor = new Color(1f, 0.85f, 0.2f, 1f);
    // 제출/패스를 지금 할 수 없을 때 버튼 투명도 (누르는 건 가능)
    [SerializeField] private float unavailableButtonAlpha = 0.5f;

    private readonly string[] secondTexts = new string[CachedSecondCount];

    private readonly StringBuilder builder = new StringBuilder(256);
    private readonly List<Tile> selectedTiles = new List<Tile>();
    private readonly List<Tile> lastPlayTiles = new List<Tile>();
    private readonly List<int> resultOrder = new List<int>();
    private int[] resultChips = new int[0];
    private int[] resultRanks = new int[0];
    private bool[] resultLeavers = new bool[0];
    private readonly List<int> settleCounts = new List<int>();
    private int[] settleChanges = new int[0];
    private bool isSettling;
    private Combination lastPlayCombination;
    private readonly CombinationText combinationText = new CombinationText();
    private readonly NicknameFitter nicknameFitter = new NicknameFitter();
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
    // 내 차례 알림용: 원래 색/이미지 (Start에서 씬 값을 보관)
    private Color myInfoDefaultColor = Color.white;
    private Image timerFillImage;
    private Sprite timerDefaultSprite;
    private Quaternion timerDefaultRotation = Quaternion.identity;
    private bool isTimerWarning;
    // 타이머 슬라이더 비율 계산용: 지금 보고 있는 마감 시각과, 그 마감을 처음 본 순간의 남은 시간
    // 서버의 턴/정리 시간 길이는 동기화되지 않으므로 마감 시각이 바뀌는 순간의 남은 시간을 전체 길이로 씀
    private double timerEndTime = -1;
    private float timerTotalSeconds = 1f;
    private Image[] seatLabelImages = new Image[0];
    private readonly Vector3[] avoidCorners = new Vector3[4];
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
        // Setup을 다시 돌리기 전 씬(팀원 씬 포함)에는 이 버튼이 없을 수 있음
        if (clearSelectionButton != null)
        {
            clearSelectionButton.onClick.AddListener(OnClearSelectionClicked);
        }
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

        // 표시 전용 (플레이어가 끌어서 바꾸지 못하게)
        if (timerSlider != null)
        {
            timerSlider.interactable = false;
            if (timerSlider.fillRect != null)
            {
                timerFillImage = timerSlider.fillRect.GetComponent<Image>();
            }
        }
        if (timerFillImage != null)
        {
            timerDefaultSprite = timerFillImage.sprite;
        }
        if (timerRoot != null)
        {
            timerDefaultRotation = timerRoot.localRotation;
        }
        if (myInfoPanel != null)
        {
            myInfoDefaultColor = myInfoPanel.color;
        }
        seatLabelImages = new Image[seatLabels.Length];
        for (int i = 0; i < seatLabels.Length; i++)
        {
            seatLabelImages[i] = seatLabels[i].GetComponent<Image>();
        }
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
                UpdateTimer(table, local.Seat);
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
            RefreshResult(table, mySeat);
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
            builder.Append(i + 1).Append(". ").Append(nicknameFitter.Shorten(table.Seats[i].nickname)).Append('\n');
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
        roundText.text = "라운드 <color=#FF0000>" + table.RoundNumber + "</color> / " + table.RoundCount;

        // 차례 배너에 정산 시간을 표시해야 하므로 정산 여부를 먼저 계산
        RefreshSettle(table);

        if (isSettling)
        {
            SetTurnText("", "정산 시간");
        }
        else if (table.IsPreparing)
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
            SetTurnText("", "패 정리 시간");
            if (readyCountText != null)
            {
                builder.Clear();
                builder.Append("준비 ").Append(readyCount).Append(" / ").Append(humanCount);
                readyCountText.text = builder.ToString();
            }
        }
        else if (table.Phase == GamePhase.Playing && table.CurrentSeat >= 0)
        {
            string turnName = "";
            string turnSuffix = "내 차례";
            if (table.CurrentSeat != mySeat)
            {
                turnName = GetNickname(table, table.CurrentSeat);
                turnSuffix = " 차례";
            }

            if (table.IsLeadTurn)
            {
                turnSuffix += " (선)";
            }
            SetTurnText(turnName, turnSuffix);
        }
        else
        {
            SetTurnText("", "");
        }

        if (readyPanel != null)
        {
            SetActive(readyPanel, table.IsPreparing);
        }

        RefreshSettleBubbles(table);

        if (mySeat >= 0 && mySeat < table.Seats.Count)
        {
            SeatInfo mine = table.Seats[mySeat];
            nicknameFitter.SetText(myNameText, mine.nickname, "", 0f);
            SetNumberText(myChipText, chipView.GetShownChips(mySeat, mine.chips));
            SetNumberText(myTileText, mine.tileCount);
            SetProfile(myProfileImage, mine.profileImage);
        }
        if (myReadyIcon != null)
        {
            bool isMeReady = mySeat >= 0 && mySeat < table.Seats.Count && table.Seats[mySeat].isReady;
            SetActive(myReadyIcon, table.IsPreparing && isMeReady);
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
            // 족보 문양 숫자 (예: 스트레이트 해 1). 말풍선과 같은 형식
            nicknameFitter.SetText(lastPlayText, GetNickname(table, table.LastPlaySeat), " : " + combinationText.GetPlayText(lastPlayCombination, table.MaxNumber), 0f);
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
    // 내 차례 알림(내 정보 깜빡임, 남은 시간 경고)도 여기서 매 프레임 갱신
    private void UpdateTimer(LexioTable table, int mySeat)
    {
        int seconds = -1;
        float remaining = 0f;
        // 패 돌리는 중에는 표시 안 함 (서버 마감 시간에 연출 시간이 더해져 있음)
        if (table.Phase == GamePhase.Playing && table.CurrentSeat >= 0 && clientView.IsDealing == false)
        {
            double endTime = table.TurnDeadline;
            if (table.IsPreparing)
            {
                endTime = table.PrepareEndTime;
            }
            remaining = Mathf.Max(0f, (float)(endTime - NetworkTime.time));
            seconds = Mathf.CeilToInt(remaining);
        }

        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            timerText.text = GetSecondText(seconds);
        }

        // 슬라이더는 부드럽게 줄어들도록 매 프레임 갱신 (할당 없음)
        if (timerSlider != null)
        {
            timerSlider.normalizedValue = GetTimerRatio(table);
        }

        bool isMyTurn = mySeat >= 0 && table.Phase == GamePhase.Playing && table.IsPreparing == false && table.CurrentSeat == mySeat;
        UpdateMyTurnHighlight(isMyTurn);
        UpdateTimerWarning(isMyTurn && seconds >= 0 && seconds <= timerWarningSeconds);
    }

    // 내 차례 동안 내 정보 배경을 myTurnColor와 원래 색 사이 반쯤까지 오가게 함. 아니면 원래 색
    private void UpdateMyTurnHighlight(bool isMyTurn)
    {
        if (myInfoPanel != null)
        {
            Color color = myInfoDefaultColor;
            if (isMyTurn)
            {
                float pulse = (Mathf.Sin(Time.time * myTurnPulseSpeed) + 1f) * 0.5f;
                color = Color.Lerp(myTurnColor, myInfoDefaultColor, pulse * 0.5f);
            }
            if (myInfoPanel.color != color)
            {
                myInfoPanel.color = color;
            }
        }
    }

    // 남은 시간 경고: 켜지고 꺼질 때만 게이지 이미지를 바꾸고, 켜져 있는 동안 타이머 전체를 좌우로 흔듦 (숫자는 그대로)
    private void UpdateTimerWarning(bool isWarning)
    {
        if (isWarning != isTimerWarning)
        {
            isTimerWarning = isWarning;
            if (timerFillImage != null && timerWarningSprite != null)
            {
                if (isWarning)
                {
                    timerFillImage.sprite = timerWarningSprite;
                }
                else
                {
                    timerFillImage.sprite = timerDefaultSprite;
                }
            }
            if (isWarning == false && timerRoot != null)
            {
                timerRoot.localRotation = timerDefaultRotation;
            }
        }

        if (isWarning && timerRoot != null)
        {
            float angle = Mathf.Sin(Time.time * timerShakeSpeed) * timerShakeAngle;
            timerRoot.localRotation = timerDefaultRotation * Quaternion.Euler(0f, 0f, angle);
        }
    }

    // 남은 시간 비율. 타이머를 표시하지 않는 동안에는 0
    private float GetTimerRatio(LexioTable table)
    {
        float ratio = 0f;
        if (table.Phase == GamePhase.Playing && table.CurrentSeat >= 0 && clientView.IsDealing == false)
        {
            double endTime = table.TurnDeadline;
            if (table.IsPreparing)
            {
                endTime = table.PrepareEndTime;
            }
            float remaining = Mathf.Max(0f, (float)(endTime - NetworkTime.time));
            if (endTime != timerEndTime)
            {
                timerEndTime = endTime;
                timerTotalSeconds = Mathf.Max(0.01f, remaining);
            }
            ratio = Mathf.Clamp01(remaining / timerTotalSeconds);
        }
        else
        {
            // 다음에 타이머가 다시 보일 때 그 순간의 남은 시간을 전체 길이로 다시 잡음
            timerEndTime = -1;
        }
        return ratio;
    }

    private void RefreshSeatLabelTexts(LexioTable table, int mySeat)
    {
        for (int i = 0; i < seatNameTexts.Length; i++)
        {
            if (i < table.Seats.Count && i != mySeat)
            {
                SeatInfo info = table.Seats[i];
                if (info.isAi)
                {
                    nicknameFitter.SetText(seatNameTexts[i], info.nickname, " (AI)", 0f);
                }
                else
                {
                    nicknameFitter.SetText(seatNameTexts[i], info.nickname, "", 0f);
                }
                SetNumberText(GetArrayItem(seatChipTexts, i), chipView.GetShownChips(i, info.chips));
                SetNumberText(GetArrayItem(seatTileTexts, i), info.tileCount);
                SetProfile(GetArrayItem(seatProfileImages, i), info.profileImage);

                // 준비 아이콘은 패 정리 시간에만. 정리 시간이 끝나면(게임 시작) 모두 꺼짐
                GameObject readyIcon = GetArrayItem(seatReadyIcons, i);
                if (readyIcon != null)
                {
                    SetActive(readyIcon, table.IsPreparing && info.isReady);
                }

                bool isTurn = table.Phase == GamePhase.Playing && table.IsPreparing == false && table.CurrentSeat == i;
                SetLabelSprite(i, isTurn);
            }
        }
    }

    private void SetLabelSprite(int seat, bool isTurn)
    {
        Image labelImage = GetArrayItem(seatLabelImages, seat);
        Sprite sprite = normalLabelSprite;
        if (isTurn)
        {
            sprite = turnLabelSprite;
        }

        if (labelImage != null && sprite != null)
        {
            labelImage.sprite = sprite;
        }
    }

    // 정산 중이면 자리마다 계산식을 말풍선에 넘기고, 아니면 모두 끔
    private void RefreshSettleBubbles(LexioTable table)
    {
        if (bubbleView != null)
        {
            if (isSettling)
            {
                for (int i = 0; i < table.Seats.Count; i++)
                {
                    builder.Clear();
                    AppendSettleText(table.Seats[i], settleChanges[i]);
                    bubbleView.SetSettleText(i, builder.ToString());
                }
            }
            else
            {
                bubbleView.ClearSettleTexts();
            }
        }
    }

    // 숫자 문자열은 0~99 캐시 재사용 (칩/패 수는 상태가 바뀔 때만 호출됨)
    private void SetNumberText(Text target, int number)
    {
        if (target != null)
        {
            if (number >= 0)
            {
                target.text = GetSecondText(number);
            }
            else
            {
                target.text = number.ToString();
            }
        }
    }

    // 프로필 번호가 프로필 목록 범위를 넘으면 0번 이미지 사용. 목록이 비어 있으면 씬에 넣은 이미지 그대로 둠
    private void SetProfile(Image target, int profileImage)
    {
        if (target != null && profileSprites != null && profileSprites.Length > 0)
        {
            int index = profileImage;
            if (index < 0 || index >= profileSprites.Length)
            {
                index = 0;
            }
            target.sprite = profileSprites[index];
        }
    }

    // 배열이 비어 있거나 짧아도 안전하게 꺼냄 (씬에 없는 칸은 null)
    private T GetArrayItem<T>(T[] items, int index) where T : Object
    {
        T item = null;
        if (items != null && index >= 0 && index < items.Length)
        {
            item = items[index];
        }
        return item;
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

    // 말풍선 두 줄: "4장 × 2 = 8점" / "칩 -5" (2가 없으면 첫 줄은 "4장 = 4점")
    // 칩 변화는 얻으면 chipGainColor, 잃으면 chipLossColor (정산 때만 만들어지므로 색 문자열 할당은 괜찮음)
    private void AppendSettleText(SeatInfo info, int change)
    {
        builder.Append(info.tileCount).Append("장");
        if (info.tileCount > 0 && info.settleCount > info.tileCount)
        {
            builder.Append(" × ").Append(info.settleCount / info.tileCount).Append(" = ").Append(info.settleCount).Append("점");
        }
        else
        {
            builder.Append(" = ").Append(info.tileCount).Append("점");
        }
        builder.Append('\n');
        if (change != 0)
        {
            Color changeColor = chipLossColor;
            if (change > 0)
            {
                changeColor = chipGainColor;
            }
            builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(changeColor)).Append('>');
        }
        builder.Append("칩 ");
        if (change > 0)
        {
            builder.Append('+');
        }
        builder.Append(change);
        if (change != 0)
        {
            builder.Append("</color>");
        }
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
                        Vector3 labelPosition = GetAvoidedLabelPosition(seatLabels[i], screenPoint.x, screenPoint.y);
                        // 위치가 그대로면 다시 넣지 않음 (넣으면 캔버스를 다시 그림)
                        if (seatLabels[i].position != labelPosition)
                        {
                            seatLabels[i].position = labelPosition;
                        }
                    }
                }
            }
            SetActive(seatLabels[i].gameObject, isVisible);
        }
    }

    // 이름표(+ 위 말풍선 자리)가 labelAvoidRects와 겹치면 겹치지 않을 만큼만 옆으로 옮긴 위치를 돌려줌. 화면 밖으로는 나가지 않게 함
    // 4인일 때 맞은편 상대 이름표의 말풍선이 차례 배너를 가리지 않게 하기 위함 (좌표는 화면 픽셀)
    // 높이는 그대로 두므로 이름표가 상대 패 위에 있고, 말풍선도 그대로 이름표 위에 뜸
    // 옮기는 방향은 피할 UI의 가운데 기준으로 이름표가 있던 쪽 (정가운데면 오른쪽)
    private Vector3 GetAvoidedLabelPosition(RectTransform label, float x, float y)
    {
        Vector3 scale = label.lossyScale;
        Rect rect = label.rect;
        float halfWidth = rect.width * scale.x * 0.5f;
        float belowPivot = rect.height * scale.y * label.pivot.y;
        // 이름표 피벗에서 이름표 맨 위까지의 높이
        float labelAbovePivot = rect.height * (1f - label.pivot.y) * scale.y;
        float labelTop = y + labelAbovePivot;
        float labelBottom = y - belowPivot;
        float bubbleTop = labelTop + labelBubbleReserve * scale.y;
        float bubbleHalfWidth = labelBubbleReserveWidth * scale.x * 0.5f;
        float gap = labelAvoidGap * scale.x;
        float resultX = x;
        if (labelAvoidRects != null)
        {
            for (int i = 0; i < labelAvoidRects.Length; i++)
            {
                RectTransform avoid = labelAvoidRects[i];
                if (avoid != null)
                {
                    // 0 = 왼쪽 아래, 2 = 오른쪽 위
                    avoid.GetWorldCorners(avoidCorners);
                    float avoidLeft = avoidCorners[0].x;
                    float avoidRight = avoidCorners[2].x;
                    float avoidBottom = avoidCorners[0].y;
                    float avoidTop = avoidCorners[2].y;

                    // 이름표와 말풍선 중 실제로 겹치는 것의 절반 너비만큼만 비킴
                    float needHalfWidth = 0f;
                    bool isLabelOverlapY = labelTop > avoidBottom && labelBottom < avoidTop;
                    bool isLabelOverlapX = resultX + halfWidth > avoidLeft && resultX - halfWidth < avoidRight;
                    if (isLabelOverlapY && isLabelOverlapX)
                    {
                        needHalfWidth = halfWidth;
                    }
                    bool isBubbleOverlapY = bubbleTop > avoidBottom && labelTop < avoidTop;
                    bool isBubbleOverlapX = resultX + bubbleHalfWidth > avoidLeft && resultX - bubbleHalfWidth < avoidRight;
                    if (isBubbleOverlapY && isBubbleOverlapX)
                    {
                        needHalfWidth = Mathf.Max(needHalfWidth, bubbleHalfWidth);
                    }

                    if (needHalfWidth > 0f)
                    {
                        float avoidCenterX = (avoidLeft + avoidRight) * 0.5f;
                        if (resultX >= avoidCenterX)
                        {
                            resultX = avoidRight + gap + needHalfWidth;
                        }
                        else
                        {
                            resultX = avoidLeft - gap - needHalfWidth;
                        }
                    }
                }
            }
        }
        resultX = Mathf.Clamp(resultX, halfWidth, Screen.width - halfWidth);
        return new Vector3(resultX, y, 0f);
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

    private void OnClearSelectionClicked()
    {
        clientView.ClearSelection();
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

    // 순위 계산 후 결과 창(resultView)에 넘김. 글자 결과(resultText)가 있으면 내 줄은 노란색으로 표시
    private void RefreshResult(LexioTable table, int mySeat)
    {
        int count = table.Seats.Count;
        if (resultChips.Length != count)
        {
            resultChips = new int[count];
            resultLeavers = new bool[count];
        }
        for (int i = 0; i < count; i++)
        {
            resultChips[i] = table.Seats[i].chips;
            // 게임 도중 나간 자리만 isAi가 켜짐 (게임이 끝난 뒤 나간 사람은 이탈자가 아님)
            resultLeavers[i] = table.Seats[i].isAi;
        }
        resultRanks = ScoreCalculator.CalculateRanks(resultChips, resultLeavers);

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

        if (resultView != null)
        {
            resultView.Show(table, mySeat, resultOrder, resultRanks, resultChips, profileSprites);
        }
        if (resultText != null)
        {
            RefreshResultText(table, mySeat);
        }
    }

    // 결과 창 없이 글자로만 표시할 때 (예전 씬)
    private void RefreshResultText(LexioTable table, int mySeat)
    {
        builder.Clear();
        builder.Append("최종 순위\n\n");
        for (int i = 0; i < resultOrder.Count; i++)
        {
            int seat = resultOrder[i];
            bool isMine = seat == mySeat;
            if (isMine)
            {
                builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(myResultColor)).Append('>');
            }
            builder.Append(resultRanks[seat]).Append("위   ").Append(nicknameFitter.Shorten(table.Seats[seat].nickname));
            if (table.Seats[seat].isAi)
            {
                builder.Append(" (AI)");
            }
            builder.Append("   칩 ").Append(resultChips[seat]);
            if (isMine)
            {
                builder.Append("</color>");
            }
            builder.Append('\n');
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

    // 차례 배너 글자. 닉네임이 길면 배너 이미지 안에 들어가게 줄임 (배너가 부모가 아닌 씬에서는 글자 칸 너비 기준)
    private void SetTurnText(string nickname, string suffix)
    {
        float width = 0f;
        RectTransform banner = turnText.rectTransform.parent as RectTransform;
        if (banner != null)
        {
            width = Mathf.Min(turnText.rectTransform.rect.width, banner.rect.width - turnTextPadding * 2f);
        }
        nicknameFitter.SetText(turnText, nickname, suffix, width);
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
