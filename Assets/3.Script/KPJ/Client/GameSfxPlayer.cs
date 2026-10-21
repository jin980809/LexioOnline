using Mirror;
using UnityEngine;
using UnityEngine.UI;

// 게임에서 일어나는 일에 맞춰 SoundManager의 효과음을 냄 (SoundManager와 같은 오브젝트에 붙임)
//  - 버튼: 씬의 모든 버튼(꺼져 있는 것 포함)을 누를 때
//  - 패 내기 / 패스: 누군가(나 포함) 냈거나 패스했다는 서버 알림을 받을 때
//  - 패 받기: 새로 패를 나눠 받을 때 (같은 패를 다시 받는 경우는 제외)
//  - 칩 모으기: 라운드 정산 칩이 날아가기 시작할 때
//  - 내 차례 / 다른 사람 차례: 차례가 바뀔 때 (패 정리 시간에는 없음). 내 차례 알림음은 설정창에서 끌 수 있음
//  - 남은 시간 경고: 내 차례 남은 시간 경고가 켜질 때
//  - 에러: 게임 중 알림 문구(낼 수 없는 패 등)가 뜰 때
// 네트워크 매니저·게임 UI·칩 연출은 씬마다 없을 수도 있어서 Start에서 찾고, 없으면 그 소리만 건너뜀
[RequireComponent(typeof(SoundManager))]
public class GameSfxPlayer : MonoBehaviour
{
    private SoundManager soundManager;
    private LexioTableNetworkManager networkManager;
    private LexioUI lexioUi;
    private ChipFlightView chipView;
    // 차례 바뀜 판정용: 마지막으로 본 차례의 자리와 마감 시각 (같은 사람에게 다시 차례가 와도 마감 시각은 바뀜)
    private int lastTurnSeat = -1;
    private double lastTurnDeadline = -1;

    private void Awake()
    {
        soundManager = GetComponent<SoundManager>();
    }

    private void Start()
    {
        networkManager = NetworkManager.singleton as LexioTableNetworkManager;
        if (networkManager != null)
        {
            networkManager.PlayReceived += OnPlayReceived;
            networkManager.PassReceived += OnPassReceived;
            networkManager.HandReceived += OnHandReceived;
        }

        lexioUi = FindAnyObjectByType<LexioUI>(FindObjectsInactive.Include);
        if (lexioUi != null)
        {
            lexioUi.TimerWarningStarted += OnTimerWarningStarted;
            lexioUi.NoticeShown += OnNoticeShown;
        }

        chipView = FindAnyObjectByType<ChipFlightView>(FindObjectsInactive.Include);
        if (chipView != null)
        {
            chipView.SettleStarted += OnSettleStarted;
        }

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].onClick.AddListener(soundManager.PlayButton);
        }
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.PlayReceived -= OnPlayReceived;
            networkManager.PassReceived -= OnPassReceived;
            networkManager.HandReceived -= OnHandReceived;
        }
        if (lexioUi != null)
        {
            lexioUi.TimerWarningStarted -= OnTimerWarningStarted;
            lexioUi.NoticeShown -= OnNoticeShown;
        }
        if (chipView != null)
        {
            chipView.SettleStarted -= OnSettleStarted;
        }
    }

    private void Update()
    {
        int turnSeat = -1;
        double turnDeadline = -1;
        int mySeat = -1;
        if (networkManager != null)
        {
            LexioTable table = networkManager.Table;
            LexioPlayer local = networkManager.LocalPlayer;
            if (table != null && local != null && table.Phase == GamePhase.Playing && table.IsPreparing == false)
            {
                turnSeat = table.CurrentSeat;
                turnDeadline = table.TurnDeadline;
                mySeat = local.Seat;
            }
        }

        if (turnSeat >= 0 && (turnSeat != lastTurnSeat || turnDeadline != lastTurnDeadline))
        {
            if (turnSeat == mySeat)
            {
                soundManager.PlayTurnAlert();
            }
            else
            {
                soundManager.PlaySfx(SoundManager.Sfx.OtherTurn);
            }
        }
        lastTurnSeat = turnSeat;
        lastTurnDeadline = turnDeadline;
    }

    private void OnPlayReceived(int seat, byte[] tileIds)
    {
        soundManager.PlaySfx(SoundManager.Sfx.Put);
    }

    private void OnPassReceived(int seat)
    {
        soundManager.PlaySfx(SoundManager.Sfx.Pass);
    }

    private void OnHandReceived(byte[] tileIds, bool isNewDeal, int mySeat, int playerCount)
    {
        if (isNewDeal)
        {
            soundManager.PlaySfx(SoundManager.Sfx.Receive);
        }
    }

    private void OnSettleStarted()
    {
        soundManager.PlaySfx(SoundManager.Sfx.ChipGather);
    }

    private void OnTimerWarningStarted()
    {
        soundManager.PlaySfx(SoundManager.Sfx.TimerWarning);
    }

    private void OnNoticeShown()
    {
        soundManager.PlaySfx(SoundManager.Sfx.Error);
    }
}
