using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FontShine : MonoBehaviour, IPointerDownHandler,IPointerUpHandler
{
    public Text btnText;
    private Color oriColor;

    [Range(0f, 1f)]
    public float pressedAlpha = 0.5f;


    void Start()
    {
        if (btnText != null)
        {
            oriColor = btnText.color;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (btnText != null)
        {
            Color dim = oriColor;
            dim.a = pressedAlpha;
            btnText.color = dim;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (btnText != null)
        {
            btnText.color = oriColor;
        }
    }
}
