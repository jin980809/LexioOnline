using System;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

// NYM 통합 UI의 계정 폼과 공통 버튼. 기존 팀원 스크립트는 수정하지 않는다.
public class NYMAccountUI : MonoBehaviour
{
    [Header("통합 화면 연결")]
    [SerializeField] Transform uiRoot;
    [SerializeField] NYMAccountAuthenticator account;
    [SerializeField] NYMMatchmakingUI matchmaking;
    [SerializeField] GameObject settingsPopup;
    [Tooltip("메인 나가기 버튼이 띄울 종료 확인 팝업. 비우면 바로 종료 (2026-10-20 KPJ 요청·사용자 승인)")]
    [SerializeField] GameObject quitConfirmPopup;

    InputField signupId, signupNickname, signupPassword, signupConfirm;
    InputField changeId, currentPassword, newPassword, newConfirm, deleteId, deletePassword, editNickname;
    Button[] signupColors, editColors, actionButtons;
    Text signupFeedback, changeFeedback, deleteFeedback, editFeedback;
    Toggle deleteConfirm, saveId;
    int signupColor, editColor, nextRequestId, pendingId;
    string submittedId;
    Text pendingFeedback;
    float deadline;
    bool returnToInfo;
    int historyTab, historyRequest, historyPage;
    GameRecordEntry[] history = new GameRecordEntry[0];

    T Get<T>(string path) where T : Component => uiRoot.Find(path).GetComponent<T>();
    Transform Panel(string name) => uiRoot.Find(name + "/Window");
    bool LoggedIn => NetworkClient.isConnected && NetworkClient.connection != null && NetworkClient.connection.isAuthenticated;

    void OnEnable()
    {
        if (account == null) return;
        account.ActionResultReceived += OnActionResult;
        account.ResultReceived += OnLoginResult;
        account.RecordsReceived += OnRecords;
        account.GetComponent<LexioNetworkManager>().ClientDisconnected += OnDisconnected;
    }
    void OnDisable()
    {
        if (account == null) return;
        account.ActionResultReceived -= OnActionResult;
        account.ResultReceived -= OnLoginResult;
        account.RecordsReceived -= OnRecords;
        if (LexioNetworkManager.singleton != null) LexioNetworkManager.singleton.ClientDisconnected -= OnDisconnected;
    }

    void Start()
    {
        signupId = Get<InputField>("SignUpPopup/Window/IdInput");
        signupNickname = Get<InputField>("SignUpPopup/Window/NicknameInput");
        signupPassword = Get<InputField>("SignUpPopup/Window/PasswordInput");
        signupConfirm = Get<InputField>("SignUpPopup/Window/PasswordConfirmInput");
        changeId = Get<InputField>("PasswordChangePopup/Window/IdInput");
        currentPassword = Get<InputField>("PasswordChangePopup/Window/CurrentPasswordInput");
        newPassword = Get<InputField>("PasswordChangePopup/Window/NewPasswordInput");
        newConfirm = Get<InputField>("PasswordChangePopup/Window/NewPasswordConfirmInput");
        deleteId = Get<InputField>("AccountDeletePopup/Window/IdInput");
        deletePassword = Get<InputField>("AccountDeletePopup/Window/PasswordInput");
        deleteConfirm = Get<Toggle>("AccountDeletePopup/Window/ConfirmToggle");
        editNickname = Get<InputField>("ProfileEditPopup/Window/NicknameInput");
        signupFeedback = Get<Text>("SignUpPopup/Window/PasswordConfirmMessage");
        changeFeedback = Get<Text>("PasswordChangePopup/Window/Feedback");
        deleteFeedback = Get<Text>("AccountDeletePopup/Window/Feedback");
        editFeedback = Get<Text>("ProfileEditPopup/Window/Feedback");
        signupColors = ColorButtons("SignUpPopup", false);
        editColors = ColorButtons("ProfileEditPopup", true);
        signupNickname.onValueChanged.AddListener(_ => Preview(false));
        editNickname.onValueChanged.AddListener(_ => Preview(true));
        signupId.onValueChanged.AddListener(_ => Get<Text>("SignUpPopup/Window/IdMessage").gameObject.SetActive(false));
        Get<Button>("SignUpPopup/Window/ProfileSelect/Preview/PreviewEditButton").onClick.AddListener(() => SelectColor((signupColor + 1) % 6, false));
        Get<Button>("ProfileEditPopup/Window/ProfileSelect/Preview/PreviewEditButton").onClick.AddListener(() => SelectColor((editColor + 1) % 6, true));

        var check = Get<Button>("SignUpPopup/Window/DuplicateCheckButton");
        var register = Get<Button>("SignUpPopup/Window/SubmitButton");
        var change = Get<Button>("PasswordChangePopup/Window/ChangeButton");
        var delete = Get<Button>("AccountDeletePopup/Window/DeleteButton");
        var edit = Get<Button>("ProfileEditPopup/Window/SaveButton");
        check.onClick.AddListener(CheckId); register.onClick.AddListener(Register);
        change.onClick.AddListener(ChangePassword); delete.onClick.AddListener(DeleteAccount); edit.onClick.AddListener(SaveProfile);
        actionButtons = new[] { check, register, change, delete, edit };

        Get<Button>("MyInfoPopup/Window/AvatarEditButton").onClick.AddListener(OpenProfile);
        Get<Button>("MyInfoPopup/Window/NicknameEditButton").onClick.AddListener(OpenProfile);
        Get<Button>("MyInfoPopup/Window/PasswordChangeButton").onClick.AddListener(() => PrepareForm("PasswordChangePopup", true));
        Get<Button>("MyInfoPopup/Window/AccountDeleteButton").onClick.AddListener(() => PrepareForm("AccountDeletePopup", true));
        Get<Button>("LoginPage/LoginPanel/PasswordChangeLink").onClick.AddListener(() => PrepareForm("PasswordChangePopup", false));
        Get<Button>("LoginPage/LoginPanel/AccountDeleteLink").onClick.AddListener(() => PrepareForm("AccountDeletePopup", false));
        foreach (string popup in new[] { "SignUpPopup", "PasswordChangePopup", "AccountDeletePopup", "ProfileEditPopup" })
        {
            string name = popup;
            foreach (string button in new[] { "CloseButton", "CancelButton" })
                Get<Button>(name + "/Window/" + button).onClick.AddListener(() => CloseForm(name));
        }
        foreach (InputField input in uiRoot.GetComponentsInChildren<InputField>(true))
        {
            var eye = input.transform.Find("EyeButton");
            if (eye != null) eye.GetComponent<Button>().onClick.AddListener(() => TogglePassword(input));
        }

        saveId = Get<Toggle>("LoginPage/LoginPanel/SaveIdToggle");
        saveId.isOn = PlayerPrefs.HasKey("NYM_LoginId");
        if (saveId.isOn) Get<InputField>("LoginPage/LoginPanel/IdInput").text = PlayerPrefs.GetString("NYM_LoginId");
        saveId.onValueChanged.AddListener(value => { if (!value) PlayerPrefs.DeleteKey("NYM_LoginId"); });

        Get<Button>("MainPage/Menu/ExitButton").onClick.AddListener(() => { if (quitConfirmPopup != null) quitConfirmPopup.SetActive(true); else Quit(); });
        // KPJ 통합 씬(GameClient)은 설정 팝업을 KPJ SettingsPanel이 맡으므로 비워 둔다 (2026-10-20 KPJ 요청)
        if (settingsPopup != null)
        {
            Get<Button>("MainPage/SettingsButton").onClick.AddListener(() => settingsPopup.SetActive(true));
            foreach (Button button in GetComponentsInChildren<Button>(true))
                if (button.name == "SettingsButton") button.onClick.AddListener(() => settingsPopup.SetActive(true));
            settingsPopup.transform.Find("Window/CloseButton").GetComponent<Button>().onClick.AddListener(() => settingsPopup.SetActive(false));
            var volume = settingsPopup.transform.Find("Window/VolumeSlider").GetComponent<Slider>();
            volume.SetValueWithoutNotify(PlayerPrefs.GetFloat("NYM_Volume", 1f));
            AudioListener.volume = volume.value;
            volume.onValueChanged.AddListener(value => { AudioListener.volume = value; PlayerPrefs.SetFloat("NYM_Volume", value); VolumeLabel(value); });
            VolumeLabel(volume.value);
        }

        string[] tabs = { "AllTab", "Tab3P", "Tab4P", "Tab5P" };
        for (int i = 0; i < tabs.Length; i++) { int index = i; Get<Button>("HistoryPopup/Window/Tabs/" + tabs[i]).onClick.AddListener(() => HistoryTab(index)); }
        Get<Button>("MainPage/Menu/HistoryButton").onClick.AddListener(() => HistoryTab(historyTab));
        Get<Button>("HistoryPopup/Window/Pager/PrevButton").onClick.AddListener(() => { historyPage--; ShowHistory(null); });
        Get<Button>("HistoryPopup/Window/Pager/NextButton").onClick.AddListener(() => { historyPage++; ShowHistory(null); });
        HistoryTab(0); SelectColor(0, false); SelectColor(0, true);
    }

    Button[] ColorButtons(string popup, bool editing)
    {
        var buttons = new Button[6];
        for (int i = 0; i < 6; i++)
        {
            int color = i;
            buttons[i] = Get<Button>(popup + "/Window/ProfileSelect/AvatarGrid/Avatar" + i);
            buttons[i].onClick.AddListener(() => SelectColor(color, editing));
        }
        return buttons;
    }
    void SelectColor(int color, bool editing)
    {
        if (editing) editColor = color; else signupColor = color;
        Preview(editing);
    }
    void Preview(bool editing)
    {
        string popup = editing ? "ProfileEditPopup" : "SignUpPopup";
        int selected = editing ? editColor : signupColor;
        var colors = editing ? editColors : signupColors;
        if (colors == null) return;
        Get<Image>(popup + "/Window/ProfileSelect/Preview/PreviewAvatar").sprite = colors[selected].image.sprite;
        var label = Get<Text>(popup + "/Window/ProfileSelect/Preview/PreviewNameText");
        string name = (editing ? editNickname : signupNickname).text;
        label.supportRichText = false; label.text = string.IsNullOrWhiteSpace(name) ? "닉네임" : name;
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i].transform.Find("SelectRing").gameObject.SetActive(i == selected);
            colors[i].transform.Find("SelectCheck").gameObject.SetActive(i == selected);
        }
    }
    void Feedback(Text label, string text, bool success = false)
    {
        label.text = text; label.color = success ? new Color32(47, 153, 61, 255) : new Color32(231, 76, 60, 255);
        label.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    void Update()
    {
        if (actionButtons == null) return;
        bool available = pendingId == 0 && (!NetworkClient.active || LoggedIn);
        foreach (var button in actionButtons) button.interactable = available;
        actionButtons[3].interactable = available && deleteConfirm.isOn;
        if (pendingId != 0 && Time.unscaledTime > deadline)
        {
            pendingId = 0; Feedback(pendingFeedback, "서버 응답이 없습니다. 다시 시도하세요.");
            if (account.TemporaryConnection && NetworkClient.active) LexioNetworkManager.singleton.StopClient();
        }
    }
    void Send(AccountActionRequest request, Text feedback)
    {
        if (pendingId != 0) return;
        submittedId = request.loginId;
        request.requestId = ++nextRequestId;
        pendingId = request.requestId; pendingFeedback = feedback; deadline = Time.unscaledTime + 20f;
        if (!account.RequestAction(request)) { pendingId = 0; Feedback(feedback, "이전 요청을 처리 중입니다."); return; }
        Feedback(feedback, "처리 중...", true);
    }
    void CheckId()
    {
        string id = signupId.text.Trim().ToLowerInvariant();
        if (!NYMAccountAuthenticator.ValidId(id)) { Feedback(signupFeedback, "아이디는 1~32자로 입력하세요."); return; }
        Send(new AccountActionRequest { operation = AccountOperation.CheckId, loginId = id }, Get<Text>("SignUpPopup/Window/IdMessage"));
    }
    void Register()
    {
        if (!NYMAccountAuthenticator.ValidId(signupId.text.Trim())) { Feedback(signupFeedback, "아이디를 확인하세요."); return; }
        if (!NYMAccountAuthenticator.ValidNickname(signupNickname.text.Trim())) { Feedback(signupFeedback, "닉네임은 2~8자로 입력하세요."); return; }
        if (!NYMAccountAuthenticator.ValidNewPassword(signupPassword.text)) { Feedback(signupFeedback, "비밀번호는 1~50자로 입력하세요."); return; }
        if (signupPassword.text != signupConfirm.text) { Feedback(signupFeedback, "비밀번호가 일치하지 않습니다."); return; }
        Send(new AccountActionRequest { operation = AccountOperation.Register, loginId = signupId.text.Trim().ToLowerInvariant(), password = signupPassword.text, nickname = signupNickname.text.Trim(), profileImage = signupColor }, signupFeedback);
        signupPassword.text = signupConfirm.text = "";
    }
    void ChangePassword()
    {
        if (!NYMAccountAuthenticator.ValidNewPassword(newPassword.text)) { Feedback(changeFeedback, "새 비밀번호는 1~50자로 입력하세요."); return; }
        if (newPassword.text != newConfirm.text) { Feedback(changeFeedback, "새 비밀번호가 일치하지 않습니다."); return; }
        Send(new AccountActionRequest { operation = AccountOperation.ChangePassword, loginId = changeId.text.Trim(), password = currentPassword.text, newPassword = newPassword.text }, changeFeedback);
        currentPassword.text = newPassword.text = newConfirm.text = "";
    }
    void DeleteAccount()
    {
        if (!deleteConfirm.isOn) { Feedback(deleteFeedback, "계정 삭제 확인에 체크하세요."); return; }
        Send(new AccountActionRequest { operation = AccountOperation.DeleteAccount, loginId = deleteId.text.Trim(), password = deletePassword.text }, deleteFeedback);
        deletePassword.text = "";
    }
    void OpenProfile()
    {
        if (!LoggedIn) return;
        uiRoot.Find("MyInfoPopup").gameObject.SetActive(false);
        uiRoot.Find("ProfileEditPopup").gameObject.SetActive(true);
        editNickname.text = account.Profile.nickname; SelectColor(account.Profile.profileImage, true);
        Feedback(editFeedback, "");
    }
    void SaveProfile()
    {
        if (!NYMAccountAuthenticator.ValidNickname(editNickname.text.Trim())) { Feedback(editFeedback, "닉네임은 2~8자로 입력하세요."); return; }
        Send(new AccountActionRequest { operation = AccountOperation.UpdateProfile, nickname = editNickname.text.Trim(), profileImage = editColor }, editFeedback);
    }
    void PrepareForm(string popup, bool fromInfo)
    {
        returnToInfo = fromInfo;
        if (fromInfo) uiRoot.Find("MyInfoPopup").gameObject.SetActive(false);
        var input = popup == "PasswordChangePopup" ? changeId : deleteId;
        input.text = LoggedIn ? account.Profile.userId : Get<InputField>("LoginPage/LoginPanel/IdInput").text;
        input.readOnly = LoggedIn; deleteConfirm.isOn = false;
        Feedback(popup == "PasswordChangePopup" ? changeFeedback : deleteFeedback, "");
    }
    void CloseForm(string popup)
    {
        ClearPasswords(Panel(popup)); uiRoot.Find(popup).gameObject.SetActive(false);
        if (LoggedIn && (popup == "ProfileEditPopup" || returnToInfo)) uiRoot.Find("MyInfoPopup").gameObject.SetActive(true);
        returnToInfo = false;
    }
    void ClearPasswords(Transform root)
    {
        foreach (var input in root.GetComponentsInChildren<InputField>(true))
            if (input.transform.Find("EyeButton") != null) { input.text = ""; input.contentType = InputField.ContentType.Password; input.ForceLabelUpdate(); }
    }
    void TogglePassword(InputField input)
    {
        input.contentType = input.contentType == InputField.ContentType.Password ? InputField.ContentType.Standard : InputField.ContentType.Password;
        input.ForceLabelUpdate();
    }
    void OnActionResult(AccountActionResult result)
    {
        if (result.requestId != pendingId || pendingId == 0) return;
        pendingId = 0;
        if (result.operation == AccountOperation.CheckId && submittedId != signupId.text.Trim().ToLowerInvariant()) { Feedback(pendingFeedback, "아이디 변경 후 다시 확인하세요."); return; }
        if (result.code != AccountResultCode.Success) { Feedback(pendingFeedback, Error(result.code)); return; }
        Feedback(pendingFeedback, result.operation == AccountOperation.CheckId ? "사용 가능한 아이디입니다." : "완료했습니다.", true);
        switch (result.operation)
        {
            case AccountOperation.Register:
                Get<InputField>("LoginPage/LoginPanel/IdInput").text = submittedId;
                CloseForm("SignUpPopup"); matchmaking.SetStatus("가입 완료. 로그인하세요."); break;
            case AccountOperation.ChangePassword:
                CloseForm("PasswordChangePopup"); matchmaking.SetStatus("비밀번호를 변경했습니다."); break;
            case AccountOperation.DeleteAccount:
                if (PlayerPrefs.GetString("NYM_LoginId", "") == submittedId) PlayerPrefs.DeleteKey("NYM_LoginId");
                Get<InputField>("LoginPage/LoginPanel/IdInput").text = "";
                matchmaking.Logout(); matchmaking.SetStatus("계정을 삭제했습니다."); break;
            case AccountOperation.UpdateProfile:
                CloseForm("ProfileEditPopup"); matchmaking.SetStatus("프로필을 저장했습니다."); break;
        }
    }
    string Error(AccountResultCode code)
    {
        switch (code)
        {
            case AccountResultCode.DuplicateLoginId: return "이미 사용 중인 아이디입니다.";
            case AccountResultCode.DuplicateNickname: return "이미 사용 중인 닉네임입니다.";
            case AccountResultCode.InvalidNickname: return "닉네임은 2~8자로 입력하세요.";
            case AccountResultCode.InvalidPassword: return "비밀번호를 확인하세요.";
            case AccountResultCode.InvalidLoginId: return "아이디는 1~32자로 입력하세요.";
            case AccountResultCode.ServiceUnavailable: return "서버 DB 연결을 확인하세요.";
            case AccountResultCode.VersionMismatch: return "서버·클라이언트 버전을 맞춰주세요.";
            default: return "계정 정보를 확인하세요.";
        }
    }
    void OnDisconnected()
    {
        if (historyRequest != 0) { historyRequest = 0; ShowHistory("서버 연결이 종료되었습니다."); }
        if (pendingId == 0) return;
        pendingId = 0; Feedback(pendingFeedback, "서버 연결이 종료되었습니다.");
    }
    void OnLoginResult(AccountResultMessage result)
    {
        if (result.code != AccountResultCode.Success || saveId == null) return;
        if (saveId.isOn) PlayerPrefs.SetString("NYM_LoginId", result.profile.userId); else PlayerPrefs.DeleteKey("NYM_LoginId");
    }
    void VolumeLabel(float value) => settingsPopup.transform.Find("Window/VolumeValue").GetComponent<Text>().text = Mathf.RoundToInt(value * 100f) + "%";
    void HistoryTab(int index)
    {
        string[] names = { "AllTab", "Tab3P", "Tab4P", "Tab5P" };
        for (int i = 0; i < names.Length; i++)
        {
            var button = Get<Button>("HistoryPopup/Window/Tabs/" + names[i]);
            if (button.spriteState.selectedSprite != null) button.image.sprite = i == index ? button.spriteState.selectedSprite : button.spriteState.disabledSprite;
        }
        historyTab = index; historyPage = 0; history = new GameRecordEntry[0];
        if (historyRequest != 0) { ShowHistory("불러오는 중..."); return; } // 응답 후 OnRecords가 현재 탭으로 다시 요청
        historyRequest = LoggedIn ? account.RequestRecords(HistoryMatchSize) : 0;
        ShowHistory(historyRequest != 0 ? "불러오는 중..." : "로그인 후 전적을 볼 수 있습니다.");
    }
    int HistoryMatchSize => historyTab == 0 ? 0 : historyTab + 2;
    void OnRecords(RecordQueryResult result)
    {
        if (historyRequest == 0 || result.requestId != historyRequest) return;
        historyRequest = 0;
        if (result.matchSize != HistoryMatchSize) { HistoryTab(historyTab); return; }
        if (result.code != AccountResultCode.Success) { ShowHistory("전적을 불러오지 못했습니다."); return; }
        history = result.records ?? new GameRecordEntry[0];
        ShowHistory(history.Length == 0 ? (historyTab == 0 ? "" : HistoryMatchSize + "인 ") + "저장된 전적이 없습니다." : null);
    }
    // message가 있으면 목록 대신 안내만 표시. 행 템플릿 List/Row0..N을 페이지 단위로 채운다.
    void ShowHistory(string message)
    {
        var window = Panel("HistoryPopup");
        var list = window.Find("List");
        int rows = list.childCount, pages = Mathf.Max(1, (history.Length + rows - 1) / rows);
        historyPage = Mathf.Clamp(historyPage, 0, pages - 1);
        var empty = window.Find("EmptyText").GetComponent<Text>();
        empty.text = message ?? ""; empty.gameObject.SetActive(message != null);
        list.gameObject.SetActive(message == null);
        for (int i = 0; i < rows; i++)
        {
            var row = list.GetChild(i);
            int index = historyPage * rows + i;
            row.gameObject.SetActive(message == null && index < history.Length);
            if (!row.gameObject.activeSelf) continue;
            var record = history[index];
            row.Find("RankText").GetComponent<Text>().text = record.rank + "위 <size=20><color=#8A80A6>/ " + record.matchSize + "인</color></size>";
            // KST는 서머타임이 없어 UTC+9 고정
            row.Find("DateText").GetComponent<Text>().text = new DateTime(record.playedAtUtcTicks, DateTimeKind.Utc).AddHours(9).ToString("MM.dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            row.Find("Crown").gameObject.SetActive(record.rank == 1);
            row.Find("Players").gameObject.SetActive(false); // 전적 행에 참가자 정보 없음
            row.Find("RoundBadge/RoundText").GetComponent<Text>().text = LexioProtocol.RoundCount + "라운드";
            row.Find("ChipDeltaText").GetComponent<Text>().text = record.finalCoin.ToString(); // 증감이 아니라 정산 후 잔액
        }
        window.Find("Pager/PageText").GetComponent<Text>().text = (historyPage + 1) + " <color=#ADA5C4>/ " + pages + "</color>";
        window.Find("Pager/PrevButton").GetComponent<Button>().interactable = message == null && historyPage > 0;
        window.Find("Pager/NextButton").GetComponent<Button>().interactable = message == null && historyPage < pages - 1;
    }
    void Quit()
    {
        if (NetworkClient.active) matchmaking.Logout();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

