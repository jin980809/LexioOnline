using UnityEngine;
using UnityEngine.UI;

public class ProfilePopup : MonoBehaviour
{
    [SerializeField] private GameObject popup;
    [SerializeField] private Image profileImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Text firstText;
    [SerializeField] private Text secondText;
    [SerializeField] private Text thirdText;
    [SerializeField] private Text fourthText;
    [SerializeField] private Text playNumText;
    [SerializeField] private Text averageRankText;

    [SerializeField] private Sprite[] profileImages;

    private void Awake()
    {
        popup.SetActive(false);
    }

    public void ShowProfile(PlayerProfileData data)
    {
        if (data == null)
            return;

        nameText.text = data.userName;

        if (data.imageNum >= 0 && data.imageNum < profileImages.Length)
        {
            profileImage.sprite = profileImages[data.imageNum];
        }

        firstText.text = data.first.ToString();
        secondText.text = data.second.ToString();
        thirdText.text = data.third.ToString();
        fourthText.text = data.fourth.ToString();

        playNumText.text = data.playNum.ToString();
        averageRankText.text = data.averageRank.ToString("F2");

        popup.SetActive(true);
    }

    public void HideProfile()
    {
        popup.SetActive(false);
    }
}
