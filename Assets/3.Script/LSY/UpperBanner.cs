using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UpperBanner : MonoBehaviour
{
    [Header("텍스트 연결")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text waitingText;

    [Header("연출 설정")]
    [SerializeField] private float slideTime = 0.35f;
    [SerializeField] private float hiddenOffset = 150f;

    [Header("시연용 가짜 매칭")]
    [SerializeField] private string gameSceneName = "GameScene";   // Build Settings에 등록된 씬 이름
    [SerializeField] private float matchSeconds = 5f;              // 매칭이 잡히기까지 걸리는 시간
    [SerializeField] private bool autoFinish = true;

    private RectTransform rect;
    private Vector2 shownPos;
    private Vector2 hiddenPos;
    private Coroutine slide;
    private Coroutine fake;

    private int playerCount;
    private float elapsed;
    private int lastShownSec = -1;
    private bool matching;

    private void Awake()
    {
        rect = (RectTransform)transform;
        shownPos = rect.anchoredPosition;                  // 씬에서 배치해 둔 위치 = 내려온 위치
        hiddenPos = shownPos + Vector2.up * hiddenOffset;
        rect.anchoredPosition = hiddenPos;                 // 시작은 숨김
    }

    private void Update()
    {
        if (!matching) return;

        elapsed += Time.unscaledDeltaTime;
        int sec = (int)elapsed;
        if (sec != lastShownSec)                           // 초가 바뀔 때만 갱신
        {
            lastShownSec = sec;
            RefreshTitle(sec);
        }
    }

    // ===== 버튼에 연결하는 함수 =====
    public void OnClickMatch(int count)
    {
        if (fake != null) StopCoroutine(fake);
        Show(count);
        fake = StartCoroutine(FakeMatching(count));
    }

    public void OnClickCancel()
    {
        if (fake != null) StopCoroutine(fake);
        fake = null;
        Hide();
    }

    // ===== 배너 연출 =====
    public void Show(int count)
    {
        playerCount = count;
        elapsed = 0f;
        lastShownSec = 0;
        matching = true;
        waitingText.text = "대기 1명";
        RefreshTitle(0);
        SlideTo(shownPos);
    }

    public void Hide()
    {
        matching = false;
        SlideTo(hiddenPos);
    }

    public void SetWaiting(int waitingCount)
    {
        waitingText.text = $"대기 {waitingCount}명";
    }

    private void RefreshTitle(int totalSec)
    {
        titleText.text = $"[{playerCount}인 매칭 중 {totalSec / 60:00}:{totalSec % 60:00}]";
    }

    private void SlideTo(Vector2 target)
    {
        if (slide != null)
        {
            StopCoroutine(slide);
        }
        slide = StartCoroutine(Slide(target));
    }

    private IEnumerator Slide(Vector2 target)
    {
        Vector2 from = rect.anchoredPosition;              // 도중에 바뀌어도 현재 위치에서 이어서
        float t = 0f;
        while (t < slideTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / slideTime);
            float eased = 1f - Mathf.Pow(1f - k, 3f);      // 처음엔 빠르고 끝에 천천히 (스르륵)
            rect.anchoredPosition = Vector2.LerpUnclamped(from, target, eased);
            yield return null;
        }
        rect.anchoredPosition = target;
        slide = null;
    }

    // ===== 시연용 가짜 매칭 (서버가 생기면 이 부분을 실제 통신으로 교체) =====
    private IEnumerator FakeMatching(int count)
    {
        float step = matchSeconds / count;
        for (int waiting = 1; waiting < count; waiting++)
        {
            SetWaiting(waiting);
            yield return new WaitForSecondsRealtime(step);
        }
        if (!autoFinish)
        { 
            yield break;
        }
        SetWaiting(count);                                 // 인원이 다 참
        yield return new WaitForSecondsRealtime(0.5f);

        Hide();                                            // 배너가 올라가고
        yield return new WaitForSecondsRealtime(0.4f);

        SceneManager.LoadScene(gameSceneName);             // 게임 화면 진입
        fake = null;
    }
}