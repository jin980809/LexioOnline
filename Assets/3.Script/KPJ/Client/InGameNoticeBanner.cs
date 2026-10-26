using UnityEngine;
using UnityEngine.UI;

// 인게임 공지 띠 (GamePanel 아래 InGameNotice에 붙임). 서버 공지(ServerNoticeReceiver)가 오면 화면 맨 위로 내려와
// 공지를 passes번 흘린 뒤 다시 올라감. 게임 중에 받은 공지만 한 번 보여 주고, 메인 화면에서 이미 받은 공지는 게임에 들어와도 띄우지 않음
// 게임 시작 연출 중에 받으면 연출이 끝난 뒤 내려옴
// 띠는 클릭을 막지 않음(raycastTarget 끔). 잠깐 동안만 라운드·차례 배너 위를 가림
// 코루틴 대신 Update에서 상태로 진행 (게임이 끝나 GamePanel이 꺼지면 띠도 바로 숨김 상태로 정리됨)
public class InGameNoticeBanner : MonoBehaviour
{
    private const string NoticePrefix = "<color=#FFD54A>[공지]</color>   ";

    private enum State
    {
        Hidden,
        Waiting,
        SlidingDown,
        Scrolling,
        SlidingUp
    }

    [SerializeField] private ServerNoticeReceiver noticeReceiver;
    // 게임 시작 연출이 보이는 동안은 기다렸다가 내려옴
    [SerializeField] private GameStartIntro gameStartIntro;
    // 위아래로 움직이는 띠 (위 가장자리 기준, 피벗 위쪽)
    [SerializeField] private RectTransform bar;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private Text noticeText;
    [SerializeField] private float slideSeconds = 0.35f;
    // 초당 흐르는 거리
    [SerializeField] private float speed = 220f;
    // 공지를 몇 번 흘리고 올라갈지
    [SerializeField] private int passes = 2;
    [SerializeField] private float textGap = 100f;

    private State state = State.Hidden;
    private float stateTime;
    private int passCount;
    private float startX;
    private float endX;
    private bool isSubscribed;

    private void OnEnable()
    {
        if (noticeReceiver != null)
        {
            noticeReceiver.NoticeChanged += OnNoticeChanged;
            isSubscribed = true;
        }

        SetBarOffset(1f);
        state = State.Hidden;
    }

    private void OnDisable()
    {
        if (isSubscribed)
        {
            noticeReceiver.NoticeChanged -= OnNoticeChanged;
            isSubscribed = false;
        }
        state = State.Hidden;
        SetBarOffset(1f);
    }

    private void OnNoticeChanged(string text)
    {
        if (text.Length > 0)
        {
            Begin(text);
        }
        else if (state != State.Hidden)
        {
            // 공지를 지우면 바로 올라감
            ChangeState(State.SlidingUp);
        }
    }

    // 이미 내려와 있으면 문구만 바꿔 처음부터 다시 흘림
    private void Begin(string text)
    {
        noticeText.text = NoticePrefix + text;
        passCount = 0;
        if (state == State.Scrolling || state == State.SlidingDown)
        {
            RestartScroll();
        }
        else
        {
            ChangeState(State.Waiting);
        }
    }

    private void Update()
    {
        float elapsed = Time.unscaledTime - stateTime;
        if (state == State.Waiting)
        {
            if (gameStartIntro == null || gameStartIntro.IsPlaying == false)
            {
                RestartScroll();
                ChangeState(State.SlidingDown);
            }
        }
        else if (state == State.SlidingDown)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, slideSeconds));
            SetBarOffset(1f - EaseOutCubic(progress));
            Scroll();
            if (progress >= 1f)
            {
                ChangeState(State.Scrolling);
            }
        }
        else if (state == State.Scrolling)
        {
            Scroll();
        }
        else if (state == State.SlidingUp)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, slideSeconds));
            SetBarOffset(EaseOutCubic(progress));
            if (progress >= 1f)
            {
                ChangeState(State.Hidden);
            }
        }
    }

    private void ChangeState(State next)
    {
        state = next;
        stateTime = Time.unscaledTime;
    }

    // 글자를 오른쪽 끝 밖에서 출발시킴. 글자 폭은 레이아웃을 갱신한 뒤에 잼
    private void RestartScroll()
    {
        Canvas.ForceUpdateCanvases();
        startX = viewport.rect.width;
        endX = -noticeText.preferredWidth - textGap;
        RectTransform textRect = noticeText.rectTransform;
        textRect.anchoredPosition = new Vector2(startX, textRect.anchoredPosition.y);
    }

    // 왼쪽 끝을 다 지나가면 한 번 흐른 것. passes번 흐르면 올라감
    private void Scroll()
    {
        RectTransform textRect = noticeText.rectTransform;
        Vector2 position = textRect.anchoredPosition;
        position.x -= speed * Time.unscaledDeltaTime;
        if (position.x < endX)
        {
            passCount++;
            position.x = startX;
            if (passCount >= passes && state == State.Scrolling)
            {
                ChangeState(State.SlidingUp);
            }
        }
        textRect.anchoredPosition = position;
    }

    // 0 = 제자리(화면 맨 위에 붙음), 1 = 띠 높이만큼 위로 올라가 화면 밖
    private void SetBarOffset(float hidden)
    {
        bar.anchoredPosition = new Vector2(bar.anchoredPosition.x, bar.rect.height * hidden);
    }

    private float EaseOutCubic(float x)
    {
        return 1f - Mathf.Pow(1f - x, 3f);
    }
}
