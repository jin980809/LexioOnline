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
    // 루트가 켜진 다음 프레임에 창을 열어야 함 (OnEnable 참고)
    private bool isOpenPending;

    // 닫힘 연출 중 (창이 다 들어가면 루트가 꺼지면서 끝남)
    public bool IsClosing
    {
        get { return isClosing; }
    }

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    // 다른 코드가 루트를 직접 켜도(SetActive) 창이 튀어나오도록 켜질 때마다 창을 엶
    // 루트가 켜지는 순간에는 처음 켜지는 창(자식)의 Awake가 아직 실행되지 않아 PopOutAnimator를 바로 열 수 없으므로,
    // 그 한 프레임 동안 창을 투명하게 두었다가 Update에서 엶
    private void OnEnable()
    {
        isClosing = false;
        if (window != null)
        {
            isOpenPending = true;
            CanvasGroup windowGroup = window.GetComponent<CanvasGroup>();
            if (windowGroup != null)
            {
                windowGroup.alpha = 0f;
            }
        }
    }

    private void Update()
    {
        if (isOpenPending)
        {
            isOpenPending = false;
            window.Open();
        }

        // 창의 닫힘 연출이 끝나면(PopOutAnimator가 창을 끔) 루트도 끔
        if (isClosing && window != null && window.gameObject.activeSelf == false)
        {
            isClosing = false;
            gameObject.SetActive(false);
        }
    }

    public void Open()
    {
        if (gameObject.activeSelf)
        {
            // 닫히는 중에 다시 열면 창만 다시 엶 (열기를 기다리는 중이면 그대로 둠)
            isClosing = false;
            if (window != null && isOpenPending == false)
            {
                window.Open();
            }
        }
        else
        {
            // 켜지면 OnEnable에서 창을 엶
            gameObject.SetActive(true);
        }
    }

    public void Close()
    {
        if (gameObject.activeSelf)
        {
            // 창이 화면 계층에서 꺼져 있으면(부모가 꺼짐) 연출을 시작할 수 없으므로 바로 끔
            if (window != null && window.gameObject.activeInHierarchy && isOpenPending == false)
            {
                isClosing = true;
                window.Close();
            }
            else
            {
                // 아직 창을 열기 전이면 연출 없이 바로 끔
                isOpenPending = false;
                gameObject.SetActive(false);
            }
        }
    }
}
