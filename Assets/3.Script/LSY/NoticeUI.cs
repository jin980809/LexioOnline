using UnityEngine;
using UnityEngine.UI;

public class NoticeUI : MonoBehaviour
{
    [SerializeField] private RectTransform viewport;
    [SerializeField] private Text noticeText;
    [SerializeField] private float speed = 80f;
    [SerializeField] private float gap = 100f;

    private RectTransform textRect;
    private float startX;
    private float endX;

    private void Awake()
    {
        textRect = noticeText.rectTransform;
    }

    private void OnEnable()
    {
        Restart();
    }

    public void SetNotice(string message)
    {
        noticeText.text = message;
        Restart();
    }

    private void Restart()
    {
        Canvas.ForceUpdateCanvases();
        float viewWidth = viewport.rect.width;
        float textWidth = noticeText.preferredWidth;
        startX = viewWidth;
        endX = -textWidth - gap;
        textRect.anchoredPosition = new Vector2(startX,textRect.anchoredPosition.y);
    }

    private void Update()
    {
        Vector2 pos = textRect.anchoredPosition;
        pos.x -= speed * Time.unscaledDeltaTime;

        if (pos.x < endX)
        {
            pos.x = startX;
        }
        textRect.anchoredPosition = pos;
    }
}
