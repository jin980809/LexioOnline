using UnityEngine;
using System.Collections;
using UnityEngine.UI;

[System.Serializable]
public class TutorialPageData
{
    public string smallTitle;
    [TextArea(3, 5)]
    public string mainText;

    [Header("페이지 마다 띄울 이미지")]
    public Sprite pageImg;

    [Header("커스텀 UI")]
    public GameObject customUI;

    [Header("슬라이드쇼")]
    public GameObject[] slides;
}

public class GuideUI : MonoBehaviour
{
    [Header("UI 텍스트")]
    public Text pageNumtext;
    public Text pageTitletext;
    public Text mainText;

    [Header("UI 이미지")]
    public Image mainImage;

    [Header("UI 버튼")]
    public Button prevButton;
    public Button nextButton;

    [Header("판넬 내용")]
    public TutorialPageData[] pages;

    [Header("순서 표기할 점 이미지들")]
    public Image[] dots;
    public Sprite pastDotSprite;
    public Sprite NowDotSprite;
    public Sprite LaterDotSprite;

    [Header("슬라이드쇼")]
    [SerializeField] private float slideInterval = 1.5f;
    [SerializeField] private float fadeDuration = 0.5f;

    private Coroutine slideRoutine;

    private int pageNow = 0;

    private void OnDisable()
    {
        StopSlideShow();
    }

    private void StopSlideShow()
    {
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i].slides == null)
            {
                continue;
            }
            for (int j = 0; j < pages[i].slides.Length; j++)
            {
                if (pages[i].slides[j] != null)
                {
                    pages[i].slides[j].SetActive(false);
                }
            }
        }
    }

    private void StartSlideShow(GameObject[] slides)
    {
        StopSlideShow();

        if (slides == null || slides.Length == 0)
        {
            return;
        }
        slideRoutine = StartCoroutine(SlideShow(slides));
    }

    private IEnumerator SlideShow(GameObject[] slides)
    {
        for (int i = 0; i < slides.Length; i++)
        {
            if (slides[i] == null) continue;
            slides[i].SetActive(true);
            GetGroup(slides[i]).alpha = (i == 0) ? 1f : 0f;
        }
        int index = 0;
        while (slides.Length > 1)
        {
            yield return new WaitForSecondsRealtime(slideInterval);

            int next = (index + 1) % slides.Length;
            CanvasGroup from = GetGroup(slides[index]);
            CanvasGroup to = GetGroup(slides[next]);

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / fadeDuration);
                from.alpha = 1f - p;
                to.alpha = p;
                yield return null;
            }
            from.alpha = 0f;
            to.alpha = 1f;

            index = next;
        }
    }

    private CanvasGroup GetGroup(GameObject obj)
    {
        CanvasGroup group = obj.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = obj.AddComponent<CanvasGroup>();
        }
        return group;
    }

    public void ResetPage()
    {
        pageNow = 0;
        UpdateUI();
    }

    private void Start()
    {
        UpdateUI();
    }

    public void NextPage()
    {
        if (pages == null || pages.Length == 0)
        {
            return;
        }
        if (pageNow < pages.Length - 1)
        {
            pageNow++;
            UpdateUI();
        }
    }

    public void prevPage()
    {
        if (pages == null || pages.Length == 0)
        {
            return;
        }
        if (pageNow > 0)
        {
            pageNow--;
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (pages == null || pages.Length == 0)
        {
            return;
        }
        TutorialPageData currentData = pages[pageNow];
        if (pageNumtext != null)
        {
            pageNumtext.text = (pageNow + 1).ToString() + " / " + pages.Length.ToString();
            if (pageTitletext != null)
            {
                pageTitletext.text = currentData.smallTitle;
            }
            if (mainText != null)
            {
                mainText.text = currentData.mainText;
            }
            if (mainImage != null)
            {
                if (currentData.pageImg != null)
                {
                    mainImage.sprite = currentData.pageImg;
                    mainImage.gameObject.SetActive(true);
                }
                else
                {
                    mainImage.gameObject.SetActive(false);
                }
            }

            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i].customUI != null)
                {
                    if (i == pageNow)
                    {
                        pages[i].customUI.SetActive(true);
                    }
                    else 
                    {
                        pages[i].customUI.SetActive(false);
                    }
                }
            }

            prevButton.interactable = (pageNow > 0);
            nextButton.interactable = (pageNow < pages.Length - 1);

            for (int i = 0; i < dots.Length; i++)
            {
                if (i < pageNow)
                {
                    dots[i].sprite = pastDotSprite;
                }
                else if (i == pageNow)
                {
                    dots[i].sprite = NowDotSprite;
                }
                else
                {
                    dots[i].sprite = LaterDotSprite;
                }
                dots[i].SetNativeSize();
            }
        }
        StartSlideShow(currentData.slides);
    }
}
