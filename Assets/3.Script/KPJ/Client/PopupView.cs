using UnityEngine;
using UnityEngine.UI;

// 어두운 배경(Dim) + 창으로 된 팝업의 루트
// 창은 LSY의 PopOutAnimator로 튀어나오고 들어감. 루트(어두운 배경 포함)는 열 때 바로 켜고, 창이 다 들어간 뒤에 끔
// (PopOutAnimator를 루트에 붙이면 어두운 배경까지 작게 줄었다 커지므로 창에만 붙임)
public class PopupView : MonoBehaviour
{
    [SerializeField] private PopOutAnimator window;
    // 누르면 닫히는 버튼 (X 등). 없으면 비워 둠
    [SerializeField] private Button closeButton;

    private bool isClosing;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    // 창의 닫힘 연출이 끝나면(PopOutAnimator가 창을 끔) 루트도 끔
    private void Update()
    {
        if (isClosing && window != null && window.gameObject.activeSelf == false)
        {
            isClosing = false;
            gameObject.SetActive(false);
        }
    }

    public void Open()
    {
        isClosing = false;
        gameObject.SetActive(true);
        if (window != null)
        {
            window.Open();
        }
    }

    public void Close()
    {
        if (gameObject.activeSelf)
        {
            if (window != null && window.gameObject.activeSelf)
            {
                isClosing = true;
                window.Close();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
