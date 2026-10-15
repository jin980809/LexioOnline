using Mirror;
using UnityEngine;
using UnityEngine.UI;

// NYM 테스트 씬 전용 임시 UI. 팀원 UI/서버 원본과 큐 규약은 변경하지 않는다.
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

    [Header("매칭 대기 표시")]
    [SerializeField] GameObject queuePanel;
    [SerializeField] Text queueText;
    [SerializeField] Text queueCountText;
    [SerializeField] RectTransform spinner;
    [SerializeField] Button cancelButton;

    bool choosing;
    bool joinSent;
    bool queued;
    bool cancelPending;
    bool manualDisconnect;
    int matchSize;
    int shownSeconds = -1;
    float queueStartedAt;
    string message = "";

    void OnEnable()
    {
        networkManager.QueueUpdatedReceived += OnQueueUpdated;
        networkManager.QueueResultReceived += OnQueueResult;
        networkManager.ClientDisconnected += OnDisconnected;
    }

    void OnDisable()
    {
        if (networkManager == null) return;
        networkManager.QueueUpdatedReceived -= OnQueueUpdated;
        networkManager.QueueResultReceived -= OnQueueResult;
        networkManager.ClientDisconnected -= OnDisconnected;
    }

    void Start()
    {
        nicknameInput.characterLimit = LexioPlayer.MaxNicknameLength;
        connectButton.onClick.AddListener(ShowModes);
        threeButton.onClick.AddListener(() => Join(3));
        fourButton.onClick.AddListener(() => Join(4));
        fiveButton.onClick.AddListener(() => Join(5));
        backButton.onClick.AddListener(Back);
        cancelButton.onClick.AddListener(Cancel);
    }

    void LateUpdate()
    {
        bool inMatch = NetworkClient.isConnected && networkManager.Table != null && networkManager.LocalPlayer != null;
        if (inMatch)
        {
            choosing = false;
            ResetRequest();
        }
        nicknameInput.gameObject.SetActive(!choosing && !inMatch);
        connectButton.gameObject.SetActive(!choosing && !inMatch);
        modePanel.SetActive(choosing && matchSize == 0 && !inMatch);
        queuePanel.SetActive(matchSize != 0 && !inMatch);
        statusText.text = message;

        if (matchSize == 0 || inMatch) return;
        if (NetworkClient.isConnected && !joinSent)
        {
            joinSent = true;
            networkManager.ClientJoinQueue(matchSize);
        }
        cancelButton.interactable = !cancelPending;
        spinner.Rotate(0f, 0f, -240f * Time.unscaledDeltaTime);
        int seconds = queued ? Mathf.Max(0, Mathf.FloorToInt(Time.unscaledTime - queueStartedAt)) : 0;
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            queueText.text = queued
                ? string.Format("[ {0}인 매칭중 {1:00}:{2:00} ]", matchSize, seconds / 60, seconds % 60)
                : string.Format("[ {0}인 접속 중 00:00 ]", matchSize);
        }
    }

    void ShowModes()
    {
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
    }

    void Join(int size)
    {
        if (!choosing || matchSize != 0 || !LexioProtocol.IsValidMatchSize(size)) return;
        manualDisconnect = false;
        matchSize = size;
        joinSent = queued = cancelPending = false;
        shownSeconds = -1;
        queueCountText.text = "서버에 연결하고 있습니다.";
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
        BeginTimer();
        queueCountText.text = "대기 " + update.waitingCount + "명";
    }

    void OnQueueResult(RequestResult result)
    {
        if (matchSize == 0 || !joinSent) return;
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
            message = "매칭 신청이 거절되었습니다. 다시 선택하세요.";
        }
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
        manualDisconnect = true;
        if (NetworkClient.active) networkManager.StopClient();
        ResetRequest();
        message = "";
    }

    void OnDisconnected()
    {
        ResetRequest();
        if (!manualDisconnect) choosing = false;
        message = manualDisconnect ? "" : "서버 연결이 종료되었습니다. 서버 상태를 확인하세요.";
        manualDisconnect = false;
    }

    void ResetRequest()
    {
        matchSize = 0;
        joinSent = queued = cancelPending = false;
        shownSeconds = -1;
    }
}
