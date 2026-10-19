using UnityEngine;
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
}

public class MenuUI : MonoBehaviour
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

    private int pageNow = 0;

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
    }
}
