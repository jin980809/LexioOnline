using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class FontFly : MonoBehaviour
{
    [Header("연출 설정")]
    public float waitTime = 1.0f;
    public float flySpeed = 100f;
    public float fadeTime = 1.5f;

    private CanvasGroup group;

    private void OnEnable()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 1f;
        StartCoroutine(Fly());
    }
    public IEnumerator Fly()
    {
        yield return new WaitForSeconds(waitTime);
        float elapsedTime = 0f;
        while (elapsedTime < fadeTime)
        {
            elapsedTime += Time.deltaTime;
            transform.Translate(Vector3.up * flySpeed * Time.deltaTime, Space.World);
            group.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeTime);
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
