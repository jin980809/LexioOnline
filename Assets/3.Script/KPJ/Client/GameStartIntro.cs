using Mirror;
using UnityEngine;
using UnityEngine.UI;

// 매칭이 잡혀 경기 테이블에 들어가는 순간 "게임 시작!" 화면을 잠깐 보여 줌
//  - 어두운 배경 + 천천히 도는 빛줄기 + 튀어나오는 배너 + 노란 띠(라운드 수 · 인원) + 깜빡이는 반짝이
//  - holdSeconds 동안은 메인 화면을 그대로 두고(LexioUI가 IsHoldingMenu를 봄), 그 뒤 fadeSeconds 동안 사라지면서 테이블이 보임
//  - 서버는 첫 라운드 패 돌리기를 잠깐 미루므로(LexioTable.firstDealDelaySeconds) 테이블로 넘어간 뒤 패 돌리기를 처음부터 봄
// 게임 중에도 꺼지지 않는 캔버스(SharedCanvas)에 두고, 평소에는 투명하고 클릭을 막지 않음
// LexioUI·NYM 매칭 UI보다 먼저 실행해야 경기에 들어온 첫 프레임부터 메인 화면을 유지함
// (늦게 실행되면 그 한 프레임 동안 메인 화면이 꺼져 깜빡이고, 그때 닫히는 팝업의 닫힘 연출이 시작되지 못함)
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CanvasGroup))]
public class GameStartIntro : MonoBehaviour
{
    [SerializeField] private RectTransform rays;
    [SerializeField] private RectTransform banner;
    [SerializeField] private RectTransform band;
    [SerializeField] private Text infoText;
    [SerializeField] private RectTransform[] sparkles;
    [SerializeField] private float holdSeconds = 1f;
    [SerializeField] private float fadeSeconds = 0.25f;
    [SerializeField] private float raysDegreesPerSecond = 25f;
    // 배너·띠가 튀어나오는 시간과 시작 크기 (띠는 popDelay만큼 늦게)
    [SerializeField] private float popSeconds = 0.3f;
    [SerializeField] private float popDelay = 0.12f;
    [SerializeField] private float popStartScale = 0.6f;

    private CanvasGroup canvasGroup;
    private LexioTableNetworkManager networkManager;
    private bool wasInMatch;
    private bool isPlaying;
    private float startTime;
    private int shownRounds = -1;
    private int shownPlayers = -1;

    // 시작 화면이 보이는 동안 (사라지는 중 포함). 패 돌리기 연출은 이게 끝난 뒤 시작
    public bool IsPlaying
    {
        get { return isPlaying; }
    }

    // 시작 화면을 보여 주는 동안(사라지기 전까지) 메인 화면을 유지해야 함
    public bool IsHoldingMenu
    {
        get { return isPlaying && Time.unscaledTime - startTime < holdSeconds; }
    }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        SetVisible(0f);
    }

    private void Start()
    {
        networkManager = NetworkManager.singleton as LexioTableNetworkManager;
    }

    private void Update()
    {
        bool isInMatch = false;
        LexioTable table = null;
        if (networkManager != null)
        {
            table = networkManager.Table;
            isInMatch = NetworkClient.isConnected && table != null && networkManager.LocalPlayer != null && table.Phase != GamePhase.Finished;
        }
        // 경기에 막 들어온 순간에만 시작 (결과 화면 뒤 다음 경기도 새로 들어오면 다시 보임)
        if (isInMatch && wasInMatch == false)
        {
            isPlaying = true;
            startTime = Time.unscaledTime;
            shownRounds = -1;
            shownPlayers = -1;
        }
        wasInMatch = isInMatch;

        if (isPlaying)
        {
            float elapsed = Time.unscaledTime - startTime;
            if (elapsed >= holdSeconds + fadeSeconds || isInMatch == false)
            {
                isPlaying = false;
                SetVisible(0f);
            }
            else
            {
                Animate(elapsed);
                if (table != null)
                {
                    UpdateInfo(table.RoundCount, table.Seats.Count);
                }
            }
        }
    }

    private void Animate(float elapsed)
    {
        float alpha = 1f;
        if (elapsed < fadeSeconds)
        {
            alpha = elapsed / fadeSeconds;
        }
        else if (elapsed > holdSeconds)
        {
            alpha = 1f - (elapsed - holdSeconds) / fadeSeconds;
        }
        SetVisible(alpha);

        if (rays != null)
        {
            rays.localRotation = Quaternion.Euler(0f, 0f, -elapsed * raysDegreesPerSecond);
        }
        SetPopScale(banner, elapsed);
        SetPopScale(band, elapsed - popDelay);
        for (int i = 0; i < sparkles.Length; i++)
        {
            if (sparkles[i] != null)
            {
                // 반짝이마다 박자를 어긋나게 해서 번갈아 깜빡임
                float twinkle = 0.6f + 0.4f * Mathf.Sin(elapsed * 12f + i * 1.7f);
                sparkles[i].localScale = new Vector3(twinkle, twinkle, 1f);
            }
        }
    }

    private void SetPopScale(RectTransform target, float time)
    {
        if (target != null)
        {
            float progress = Mathf.Clamp01(time / popSeconds);
            float scale = Mathf.LerpUnclamped(popStartScale, 1f, EaseOutBack(progress));
            target.localScale = new Vector3(scale, scale, 1f);
        }
    }

    // PopOutAnimator(LSY)와 같은 튕기는 곡선
    private float EaseOutBack(float x)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    // 값이 바뀔 때만 글자를 새로 만듦
    private void UpdateInfo(int rounds, int players)
    {
        if (infoText != null && (rounds != shownRounds || players != shownPlayers))
        {
            shownRounds = rounds;
            shownPlayers = players;
            infoText.text = rounds + "라운드 · " + players + "인";
        }
    }

    private void SetVisible(float alpha)
    {
        canvasGroup.alpha = alpha;
        canvasGroup.blocksRaycasts = alpha > 0f;
        canvasGroup.interactable = false;
    }
}
