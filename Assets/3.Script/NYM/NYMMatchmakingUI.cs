using Mirror;
using UnityEngine;
using UnityEngine.UI;

// NYM 통합 씬의 DB 로그인·매칭 표시. 팀원 UI/서버 원본은 변경하지 않는다.
public class NYMMatchmakingUI : MonoBehaviour
{
    [Header("매칭 연결")]
    [Tooltip("NYM 파생 네트워크 매니저. 자동 큐 신청은 0으로 설정")]
    [SerializeField] LexioNetworkManager networkManager;
    [Tooltip("기존 닉네임 입력과 접속 버튼. 접속 버튼은 인원 선택만 표시")]
    [SerializeField] InputField nicknameInput;
    [SerializeField] Button connectButton;
    [SerializeField] Text statusText;

    [Header("인원 선택")]
    [SerializeField] GameObject modePanel;
    [SerializeField] Button threeButton;
    [SerializeField] Button fourButton;
    [SerializeField] Button fiveButton;
    [SerializeField] Button backButton;
    [Tooltip("인원 카드를 고른 뒤 누르는 매칭 시작 버튼. 비우면 카드를 누르는 즉시 신청 (2026-10-20 KPJ 요청·사용자 승인)")]
    [SerializeField] Button startButton;
    [Tooltip("3/4/5인 카드 순서의 기본·선택 이미지. 비우면 이미지를 바꾸지 않음")]
    [SerializeField] Sprite[] cardDefaultSprites = new Sprite[0];
    [SerializeField] Sprite[] cardSelectedSprites = new Sprite[0];
    [Tooltip("선택한 카드의 크기 배율")]
    [SerializeField] float selectedCardScale = 1.09f;

    [Header("매칭 대기 표시")]
    [SerializeField] GameObject queuePanel;
    [SerializeField] Text queueText;
    [SerializeField] Text queueCountText;
    [Tooltip("왼쪽부터 타일4개. 회전 없이 순차 상하 이동")]
    [SerializeField] RectTransform[] loadingTiles = new RectTransform[0];
    [SerializeField] Button cancelButton;
    [SerializeField] Text queueTimerText;
    [Tooltip("Slot0~4 순서. 선택 인원만 가운데 정렬해 표시")]
    [SerializeField] RectTransform[] queueSlots = new RectTransform[0];
    [SerializeField] Sprite emptySlotSprite;
    [SerializeField] Sprite joinedSlotSprite;
    [SerializeField] Sprite[] avatarSprites = new Sprite[0];
    [SerializeField] Image[] profileAvatars = new Image[0];

    [Header("팝업과 배경")]
    [SerializeField] CanvasGroup menuGroup;
    [SerializeField] GameObject[] popupPanels = new GameObject[0];
    [SerializeField] GameObject popupLogo;
    [SerializeField] RuleBookPanel guideDrawer;
    [Tooltip("프런트 화면에서 숨기고 실제 경기에서 표시할 테이블/배경")]
    [SerializeField] GameObject[] gameEnvironment = new GameObject[0];

    [Header("DB 로그인")]
    [SerializeField] NYMAccountAuthenticator accountAuthenticator;
    [SerializeField] GameObject loginPage;
    [SerializeField] CanvasGroup loginGroup;
    [SerializeField] GameObject mainPage;
    [SerializeField] InputField loginIdInput;
    [SerializeField] InputField loginPasswordInput;
    [SerializeField] Button loginButton;
    [SerializeField] Button logoutButton;
    [SerializeField] Text accountText;
    [Tooltip("체크하면 로그인·메인 화면 켜고 끄기를 다른 컴포넌트(KPJ FrontPageTransition, 전환 연출)가 맡음. 끄면 기존처럼 여기서 바로 전환 (2026-10-23 KPJ 요청·사용자 승인)")]
    [SerializeField] bool externalPageSwitch;

    bool choosing;
    bool joinSent;
    bool queued;
    bool cancelPending;
    bool manualDisconnect;
    int matchSize;
    int pickedSize;
    int shownSeconds = -1;
    int waitingCount;
    float queueStartedAt;
    string message = "";
    string[] waitingNicknames = new string[0];
    int[] waitingProfiles = new int[0];
    Vector2[] loadingTilePositions;
    bool authRejected;

    void OnEnable()
    {
        networkManager.QueueUpdatedReceived += OnQueueUpdated;
        networkManager.QueueResultReceived += OnQueueResult;
        networkManager.ClientDisconnected += OnDisconnected;
        if (accountAuthenticator != null)
        {
            accountAuthenticator.ResultReceived += OnLoginResult;
            accountAuthenticator.ProfileChanged += ApplyProfile;
        }
    }

    void OnDisable()
    {
        if (networkManager == null) return;
        networkManager.QueueUpdatedReceived -= OnQueueUpdated;
        networkManager.QueueResultReceived -= OnQueueResult;
        networkManager.ClientDisconnected -= OnDisconnected;
        if (accountAuthenticator != null)
        {
            accountAuthenticator.ResultReceived -= OnLoginResult;
            accountAuthenticator.ProfileChanged -= ApplyProfile;
        }
    }

    void Start()
    {
        nicknameInput.characterLimit = LexioPlayer.MaxNicknameLength;
        connectButton.onClick.AddListener(ShowModes);
        threeButton.onClick.AddListener(() => Pick(3));
        fourButton.onClick.AddListener(() => Pick(4));
        fiveButton.onClick.AddListener(() => Pick(5));
        if (startButton != null) startButton.onClick.AddListener(() => Join(pickedSize));
        backButton.onClick.AddListener(Back);
        cancelButton.onClick.AddListener(Cancel);
        loadingTilePositions = new Vector2[loadingTiles.Length];
        for (int i = 0; i < loadingTiles.Length; i++) loadingTilePositions[i] = loadingTiles[i].anchoredPosition;
        if (accountAuthenticator != null)
        {
            loginButton.onClick.AddListener(Login);
            logoutButton.onClick.AddListener(Logout);
            nicknameInput.readOnly = true;
        }
    }

    void Login()
    {
        if (NetworkClient.active) return;
        if (string.IsNullOrWhiteSpace(loginIdInput.text) || string.IsNullOrEmpty(loginPasswordInput.text))
        {
            message = "아이디와 비밀번호를 입력하세요.";
            return;
        }
        authRejected = false;
        accountAuthenticator.SetCredentials(loginIdInput.text, loginPasswordInput.text);
        loginPasswordInput.text = "";
        message = "로그인 중...";
        networkManager.StartClient();
    }

    void OnLoginResult(AccountResultMessage result)
    {
        authRejected = result.code != AccountResultCode.Success;
        if (!authRejected)
        {
            ApplyProfile(result.profile);
            message = "";
        }
        else
        {
            message = result.code == AccountResultCode.ServiceUnavailable ? "서버 DB 연결을 확인하세요."
                : result.code == AccountResultCode.VersionMismatch ? "서버·클라이언트 버전을 맞춰주세요."
                : result.code == AccountResultCode.AlreadyLoggedIn ? "이미 접속 중인 계정입니다."
                : "로그인 정보를 확인하세요.";
        }
    }

    public void SetStatus(string text) => message = text;

    void ApplyProfile(PlayerProfile profile)
    {
        nicknameInput.text = profile.nickname;
        networkManager.PendingNickname = profile.nickname;
        networkManager.PendingProfileImage = profile.profileImage;
        accountText.text = profile.userId;
        foreach (Image image in profileAvatars) if (image != null) image.sprite = Avatar(profile.profileImage);
        var card = mainPage.transform.Find("ProfileCard");
        card.Find("IdText").GetComponent<Text>().text = profile.userId;
        card.Find("ChipText").GetComponent<Text>().text = profile.currentCoin.ToString("#,0.##");
        var window = accountText.transform.parent;
        window.Find("NicknameText").GetComponent<Text>().text = profile.nickname;
        window.Find("TotalGamesStat/ValueText").GetComponent<Text>().text = profile.gamesPlayed + "판";
        window.Find("FirstPlaceStat/ValueText").GetComponent<Text>().text = profile.wins + "회";
        window.Find("FirstRateStat/ValueText").GetComponent<Text>().text = profile.gamesPlayed == 0 ? "—" : profile.winRate.ToString("0.#") + "%";
        window.Find("ChipBox/ChipText").GetComponent<Text>().text = profile.currentCoin.ToString("0.##");
        window.Find("BestChipStat/ValueText").GetComponent<Text>().text = profile.maxCoin.ToString("0.##");
        ModeRow(window, "Row3P", profile.threePlayNum, profile.threeFirst);
        ModeRow(window, "Row4P", profile.fourPlayNum, profile.fourFirst);
        ModeRow(window, "Row5P", profile.fivePlayNum, profile.fiveFirst);
    }

    // 막대 = 해당 모드 1등 비율
    static void ModeRow(Transform window, string row, int games, int first)
    {
        window.Find(row + "/GamesText").GetComponent<Text>().text = games + "판";
        window.Find(row + "/FirstText").GetComponent<Text>().text = first + "회";
        window.Find(row + "/Bar").GetComponent<Slider>().normalizedValue = games == 0 ? 0f : (float)first / games;
    }

    public Sprite Avatar(int index) => index >= 0 && index < avatarSprites.Length ? avatarSprites[index] : joinedSlotSprite;

    public void Logout()
    {
        foreach (GameObject panel in popupPanels) if (panel != null) panel.SetActive(false);
        choosing = false;
        manualDisconnect = true;
        networkManager.StopClient();
        ResetRequest();
    }

    void LateUpdate()
    {
        if (accountAuthenticator != null)
        {
            bool loggedIn = NetworkClient.isConnected && NetworkClient.connection != null && NetworkClient.connection.isAuthenticated;
            if (!externalPageSwitch)
            {
                loginPage.SetActive(!loggedIn);
                mainPage.SetActive(loggedIn);
            }
            loginButton.interactable = !NetworkClient.active;
        }
        bool inMatch = NetworkClient.isConnected && networkManager.Table != null && networkManager.LocalPlayer != null;
        if (inMatch)
        {
            choosing = false;
            if (matchSize != 0) ResetRequest();
        }
        // 인원 선택·대기 창이 떠 있어도 메인의 닉네임과 매치메이킹 버튼은 그대로 보인다 (2026-10-22 KPJ 요청·사용자 승인)
        nicknameInput.gameObject.SetActive(!inMatch);
        connectButton.gameObject.SetActive(!inMatch);
        // 닫기 버튼으로 선택을 취소할 때도 닫힘 모션(PopupView). 인원을 골라 매칭 중 창으로 바뀔 때만 바로 끔 (두 창이 겹쳐 보이지 않게)
        SetPanel(modePanel, choosing && matchSize == 0 && !inMatch, inMatch || matchSize == 0);
        SetPanel(queuePanel, matchSize != 0 && !inMatch, inMatch);
        statusText.text = message.Length == 0 ? (accountAuthenticator == null ? "게스트 테스트 · v0.1.0" : "DB 계정 로그인 · v0.1.0") : message;

        bool popupOpen = guideDrawer != null && guideDrawer.IsOpen;
        foreach (GameObject panel in popupPanels)
            if (panel != null && panel.activeInHierarchy) popupOpen = true;
        if (menuGroup != null)
        {
            menuGroup.alpha = popupOpen ? 0f : 1f;
            menuGroup.interactable = menuGroup.blocksRaycasts = !popupOpen;
        }
        if (loginGroup != null)
        {
            loginGroup.alpha = popupOpen ? 0f : 1f;
            loginGroup.interactable = loginGroup.blocksRaycasts = !popupOpen;
        }
        if (popupLogo != null) popupLogo.SetActive(popupOpen && !inMatch);
        foreach (GameObject item in gameEnvironment)
            if (item != null) item.SetActive(inMatch);

        if (matchSize == 0 || inMatch) return;
        if (NetworkClient.isConnected && !joinSent)
        {
            joinSent = true;
            networkManager.ClientJoinQueue(matchSize);
        }
        cancelButton.interactable = !cancelPending;
        for (int i = 0; i < loadingTiles.Length; i++)
            loadingTiles[i].anchoredPosition = loadingTilePositions[i] + Vector2.up * (Mathf.Sin(Time.unscaledTime * 4f - i * 0.8f) * 16f);
        int seconds = queued ? Mathf.Max(0, Mathf.FloorToInt(Time.unscaledTime - queueStartedAt)) : 0;
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            queueText.text = string.Format("{0}인 게임 · {1}", matchSize, queued ? "상대를 찾고 있어요" : "서버 연결 중");
            if (queueTimerText != null)
                queueTimerText.text = string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60);
        }
    }

    // 경기에 들어가며 닫을 때는 KPJ PopupView 닫힘 모션으로 닫아, 그동안 "게임 시작!" 화면이 겹쳐 나타나게 한다 (2026-10-22 KPJ 요청·사용자 승인).
    // 그 밖에는 기존처럼 바로 켜고 끈다. 닫히는 중에는 다시 부르지 않는다(매 프레임 다시 닫으면 모션이 처음부터 반복됨).
    void SetPanel(GameObject panel, bool show, bool animateClose)
    {
        if (show) panel.SetActive(true);
        else if (panel.activeSelf)
        {
            PopupView view = animateClose ? panel.GetComponent<PopupView>() : null;
            if (view == null) panel.SetActive(false);
            else if (!view.IsClosing) view.Close();
        }
    }

    void RefreshSlots()
    {
        for (int i = 0; i < queueSlots.Length; i++)
        {
            RectTransform slot = queueSlots[i];
            if (slot == null) continue;
            slot.gameObject.SetActive(i < matchSize);
            if (i >= matchSize) continue;
            slot.anchoredPosition = new Vector2((i - (matchSize - 1) * 0.5f) * 156f, 0f);
            bool occupied = queued && i < waitingCount;
            slot.Find("Avatar").GetComponent<Image>().sprite = occupied ? Avatar(i < waitingProfiles.Length ? waitingProfiles[i] : 0) : emptySlotSprite;
            Text label = slot.Find("NameText").GetComponent<Text>();
            label.supportRichText = false;
            label.text = occupied ? (i < waitingNicknames.Length ? waitingNicknames[i] : "입장 중") : "대기 중";
            label.color = occupied ? new Color32(35, 21, 54, 255) : new Color32(189, 181, 211, 255);
            Transform meTag = slot.Find("MeTag");
            if (meTag != null) meTag.gameObject.SetActive(occupied && i == 0);
        }
        queueCountText.text = matchSize == 0 ? "" : string.Format("<color=#231536>{0}</color> / {1}명 모였어요", waitingCount, matchSize);
    }

    void ShowModes()
    {
        if (accountAuthenticator != null && (!NetworkClient.isConnected || !NetworkClient.connection.isAuthenticated)) return;
        string nickname = nicknameInput.text.Trim();
        if (nickname.Length == 0 || nickname.Length > LexioPlayer.MaxNicknameLength)
        {
            message = "닉네임을 입력하세요.";
            return;
        }
        foreach (char character in nickname)
        {
            if (char.IsControl(character))
            {
                message = "닉네임에 제어 문자를 사용할 수 없습니다.";
                return;
            }
        }
        nicknameInput.text = nickname;
        networkManager.PendingNickname = nickname;
        choosing = true;
        message = "";
        pickedSize = 0; // 열 때마다 선택 없이 시작 (시작 버튼은 카드를 고른 뒤 활성)
        RefreshCards();
    }

    // 시작 버튼이 있으면 카드는 선택만 하고, 없으면 기존처럼 바로 신청
    void Pick(int size)
    {
        if (startButton == null) { Join(size); return; }
        pickedSize = size;
        RefreshCards();
    }

    void RefreshCards()
    {
        Button[] cards = { threeButton, fourButton, fiveButton };
        for (int i = 0; i < cards.Length; i++)
        {
            bool picked = pickedSize == i + 3;
            Sprite[] sprites = picked ? cardSelectedSprites : cardDefaultSprites;
            if (i < sprites.Length && sprites[i] != null) cards[i].image.sprite = sprites[i];
            cards[i].transform.localScale = Vector3.one * (picked ? selectedCardScale : 1f);
        }
        if (startButton != null) startButton.interactable = LexioProtocol.IsValidMatchSize(pickedSize);
    }

    void Join(int size)
    {
        if (!choosing || matchSize != 0 || !LexioProtocol.IsValidMatchSize(size)) return;
        manualDisconnect = false;
        matchSize = size;
        joinSent = queued = cancelPending = false;
        shownSeconds = -1;
        waitingCount = 0;
        RefreshSlots();
        message = "";
        if (!NetworkClient.active) networkManager.StartClient();
    }

    void BeginTimer()
    {
        if (queued) return;
        queued = true;
        queueStartedAt = Time.unscaledTime;
        shownSeconds = -1;
    }

    void OnQueueUpdated(QueueUpdated update)
    {
        if (update.matchSize != matchSize || matchSize == 0) return;
        waitingCount = Mathf.Clamp(update.waitingCount, 0, matchSize);
        waitingNicknames = update.nicknames ?? new string[0];
        waitingProfiles = update.profileImages ?? new int[0];
        BeginTimer();
        RefreshSlots();
    }

    void OnQueueResult(RequestResult result)
    {
        if (result.requestId == 0 && result.reason == RejectReason.MatchEnded) // 서버 통보: 경기 중 인원 부족으로 중단
        {
            message = "참가자가 2명 이하로 줄어 경기가 중단되었습니다. 입장료는 반환됩니다."; // 안내 팝업은 KPJ MatchEndedNotice
            return;
        }
        if (matchSize == 0 || !joinSent) return;
        if (result.requestId == 0) // 서버 통보: 정원은 모였지만 입장료 처리 실패
        {
            cancelPending = false;
            ResetRequest();
            message = RejectText(result.reason);
            return;
        }
        if (cancelPending)
        {
            cancelPending = false;
            if (result.accepted)
            {
                ResetRequest();
                message = "매칭을 취소했습니다.";
            }
            else
            {
                message = "경기 시작이 확정되었거나 취소할 수 없는 상태입니다.";
            }
            return;
        }
        if (result.accepted)
        {
            BeginTimer();
        }
        else
        {
            ResetRequest();
            message = RejectText(result.reason);
        }
    }

    static string RejectText(RejectReason reason)
    {
        if (reason == RejectReason.InsufficientCoin) return string.Format("토큰이 부족합니다. 경기 입장에 {0} 토큰이 필요합니다.", LexioProtocol.EntryFee);
        if (reason == RejectReason.ServiceUnavailable) return "서버 DB 오류로 경기를 시작하지 못했습니다. 다시 시도하세요.";
        if (reason == RejectReason.SettlementPending) return "이전 경기 정산을 처리하는 중입니다. 잠시 후 다시 시도하세요.";
        return "매칭 신청이 거절되었습니다. 다시 선택하세요.";
    }

    void Cancel()
    {
        if (matchSize == 0 || cancelPending) return;
        if (NetworkClient.isConnected && queued)
        {
            cancelPending = true;
            networkManager.ClientCancelQueue();
        }
        else
        {
            manualDisconnect = true;
            if (NetworkClient.active) networkManager.StopClient();
            ResetRequest();
            message = "";
        }
    }

    void Back()
    {
        choosing = false;
        if (accountAuthenticator == null)
        {
            manualDisconnect = true;
            if (NetworkClient.active) networkManager.StopClient();
        }
        ResetRequest();
        message = "";
    }

    void OnDisconnected()
    {
        ResetRequest();
        if (accountAuthenticator != null && accountAuthenticator.TemporaryConnection) return;
        if (!manualDisconnect) choosing = false;
        if (!authRejected) message = manualDisconnect ? "" : "서버 연결이 종료되었습니다. 서버 상태를 확인하세요.";
        manualDisconnect = false;
    }

    void ResetRequest()
    {
        matchSize = 0;
        joinSent = queued = cancelPending = false;
        shownSeconds = -1;
        waitingCount = 0;
        waitingNicknames = new string[0];
        waitingProfiles = new int[0];
        RefreshSlots();
    }
}
