using UnityEngine;

// 게임 화면에 들어올 때 인게임 UI가 화면 가장자리에서 미끄러져 들어오는 연출 (GamePanel에 붙임)
// GamePanel이 켜질 때마다(경기에 들어올 때) 각 UI를 자기 자리에서 offsets만큼 떨어진 곳에 두었다가,
// 순서대로(stagger) 살짝 튕기며 제자리로 돌아오게 함. 연출이 끝나면 위치를 정확히 원래 값으로 맞춤
// 위치를 코드가 매 프레임 바꾸는 UI(상대 이름표 등)는 넣지 않음
public class GameHudIntro : MonoBehaviour
{
    [SerializeField] private RectTransform[] targets;
    // targets와 같은 순서. 시작 위치 = 제자리 + 이 값 (예: 왼쪽에서 들어오면 x를 음수로)
    [SerializeField] private Vector2[] offsets;
    [SerializeField] private float slideSeconds = 0.45f;
    // 다음 UI가 출발하기까지의 간격
    [SerializeField] private float stagger = 0.06f;

    private Vector2[] homePositions;
    private float startTime;
    private bool isPlaying;

    private void Awake()
    {
        homePositions = new Vector2[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                homePositions[i] = targets[i].anchoredPosition;
            }
        }
    }

    private void OnEnable()
    {
        startTime = Time.unscaledTime;
        isPlaying = true;
        Apply(0f);
    }

    private void OnDisable()
    {
        // 중간에 꺼져도 다음에 켜질 때 제자리 기준이 흐트러지지 않게 원래 위치로
        isPlaying = false;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].anchoredPosition = homePositions[i];
            }
        }
    }

    private void Update()
    {
        if (isPlaying)
        {
            float elapsed = Time.unscaledTime - startTime;
            Apply(elapsed);
            if (elapsed >= slideSeconds + stagger * targets.Length)
            {
                isPlaying = false;
            }
        }
    }

    private void Apply(float elapsed)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                Vector2 offset = Vector2.zero;
                if (i < offsets.Length)
                {
                    offset = offsets[i];
                }
                float progress = Mathf.Clamp01((elapsed - stagger * i) / Mathf.Max(0.01f, slideSeconds));
                float ease = EaseOutBack(progress);
                targets[i].anchoredPosition = homePositions[i] + offset * (1f - ease);
            }
        }
    }

    // PopOutAnimator(LSY)와 같은 튕기는 곡선
    private float EaseOutBack(float x)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
