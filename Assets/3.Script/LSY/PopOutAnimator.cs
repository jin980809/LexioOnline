using System.Collections;
using System;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class PopOutAnimator : MonoBehaviour
{
    [Header("연출 설정")]
    [SerializeField] private PopupSettings setting;

    private float openTime
    {
        get 
        {
            if (setting != null)
            {
                return setting.openTime;
            }
            else { return 0.3f; }
        }
    }

    private float closeTime
    {
        get
        {
            if (setting != null)
            {
                return setting.closeTime;
            }
            else { return 0.16f; }
        }
    }

    private float startScale
    {
        get
        {
            if (setting != null)
            {
                return setting.startScale;
            }
            else { return 0.6f; }
        }
    }

    private CanvasGroup canvasGroup;
    private Coroutine running;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Open()
    {
        gameObject.SetActive(true);
        Play(startScale, 1f, 0f, 1f, openTime, EaseOutBack, true, null);
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        float from = transform.localScale.x;
        Play(from, startScale, canvasGroup.alpha, 0f, closeTime, EaseInBack, false,
() => gameObject.SetActive(false));
    }

    public void Toggle()
    {
        if (gameObject.activeSelf) Close();
        else Open();
    }

    private void Play(float fromScale, float toScale, float fromAlpha, float toAlpha, float duration, Func<float, float> ease, bool interactableAtEnd, Action onDone)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Animate(fromScale, toScale, fromAlpha, toAlpha, duration, ease, interactableAtEnd, onDone));
    }

    private IEnumerator Animate(float fromScale, float toScale, float fromAlpha, float toAlpha, float duration, Func<float, float> ease, bool interactableAtEnd, Action onDone)
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);

            float s = Mathf.LerpUnclamped(fromScale, toScale, ease(k));
            transform.localScale = new Vector3(s, s, 1f);
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, k);
            yield return null;
        }

        transform.localScale = new Vector3(toScale, toScale, 1f);
        canvasGroup.alpha = toAlpha;
        canvasGroup.interactable = interactableAtEnd;
        canvasGroup.blocksRaycasts = interactableAtEnd;

        running = null;
        onDone?.Invoke();
    }
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
    }

    private static float EaseInBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return c3 * x * x * x - c1 * x * x;
    }
}
