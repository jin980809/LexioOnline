using UnityEngine;
using UnityEngine.UI;

// 화면 오른쪽 족보표 서랍. 막대 버튼을 누르면 오른쪽에서 밀려 나오고, 다시 누르면 들어감
// 이 오브젝트는 화면 오른쪽 끝에 왼쪽 피벗으로 붙어 있고(닫힘 = 화면 밖), 버튼은 이 오브젝트의 왼쪽 바깥에 붙어서 같이 움직임
public class RuleBookPanel : MonoBehaviour
{
    [SerializeField] private Button toggleButton;
    [SerializeField] private float slideSeconds = 0.25f;

    private RectTransform drawer;
    private bool isOpen;
    private float progress;
    // 마지막으로 위치를 넣었을 때의 진행도와 서랍 폭 (둘 다 그대로면 위치를 다시 넣지 않음. 넣으면 캔버스를 다시 그림)
    private float appliedProgress = -1f;
    private float appliedWidth = -1f;

    public bool IsOpen
    {
        get { return isOpen; }
    }

    private void Awake()
    {
        drawer = (RectTransform)transform;
    }

    private void Start()
    {
        toggleButton.onClick.AddListener(Toggle);
    }

    // 게임 화면이 꺼지면(게임 종료/접속 종료) 닫힌 상태로 되돌림
    private void OnDisable()
    {
        isOpen = false;
        progress = 0f;
        ApplyPosition();
    }

    private void Update()
    {
        float target = 0f;
        if (isOpen)
        {
            target = 1f;
        }

        if (progress != target)
        {
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, slideSeconds);
            progress = Mathf.MoveTowards(progress, target, step);
        }
        // 화면 크기가 바뀌면 서랍 폭도 바뀌므로 폭이 바뀌었을 때도 다시 계산
        if (progress != appliedProgress || drawer.rect.width != appliedWidth)
        {
            ApplyPosition();
        }
    }

    public void Toggle()
    {
        isOpen = isOpen == false;
    }

    private void ApplyPosition()
    {
        if (drawer != null)
        {
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            Vector2 position = drawer.anchoredPosition;
            position.x = -drawer.rect.width * eased;
            drawer.anchoredPosition = position;
            appliedProgress = progress;
            appliedWidth = drawer.rect.width;
        }
    }
}
