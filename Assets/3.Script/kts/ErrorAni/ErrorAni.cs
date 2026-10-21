using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ErrorAni : MonoBehaviour
{
    [SerializeField] private Animator loginOutAni;
    [SerializeField] private CanvasGroup canvasGroup;

    private Coroutine myCoroutine;

    public void ErrorAniStart()
    {
        loginOutAni.SetTrigger("Error");

        if (myCoroutine != null)
        {
            StopCoroutine(myCoroutine);
        }
        myCoroutine = StartCoroutine(Delay_co());

    }

    private IEnumerator Delay_co()
    {
        canvasGroup.alpha = 1f;
        float duration = 1f;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, time / duration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }
}
