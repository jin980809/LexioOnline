using UnityEngine;

public class BGspin : MonoBehaviour
{
    [SerializeField] private int tickCount = 12;        // Tick 개수
    [SerializeField] private float stepTime = 0.08f;    // 한 칸 넘어가는 간격(작을수록 빠름)
    [SerializeField] private bool clockwise = true;     // 시계 방향 여부

    private RectTransform rect;
    private float timer;

    private void Awake()
    {
        rect = (RectTransform)transform;
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;                // 일시정지 중에도 돌게
        if (timer < stepTime) return;

        timer -= stepTime;
        float step = 360f / tickCount * (clockwise ? -1f : 1f);   // UI는 z가 음수일 때 시계 방향
        rect.Rotate(0f, 0f, step);
    }
}
