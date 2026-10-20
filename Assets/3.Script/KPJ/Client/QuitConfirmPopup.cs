using UnityEngine;
using UnityEngine.UI;

// 게임 종료 확인창: 취소하면 닫고, 종료하면 게임을 끔 (에디터에서는 플레이 모드를 멈춤)
// 평소에는 꺼져 있고, 다른 창(설정창 등)이 Open()으로 띄움. 열고 닫는 연출은 PopupView가 함 (없으면 바로 켜고 끔)
public class QuitConfirmPopup : MonoBehaviour
{
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private PopupView popupView;

    private void Awake()
    {
        cancelButton.onClick.AddListener(Close);
        quitButton.onClick.AddListener(QuitGame);
    }

    public void Open()
    {
        if (popupView != null)
        {
            popupView.Open();
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    public void Close()
    {
        if (popupView != null)
        {
            popupView.Close();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
