using Mirror;
using UnityEngine;
using UnityEngine.UI;

// 경기 중 남은 사람이 2명 이하가 되어 서버가 경기를 끝내면(RejectReason.MatchEnded 통보) 안내 팝업을 띄움. 확인 버튼을 누르면 닫힘
// 이 컴포넌트는 UICanvas에 붙임. 게임 중에는 메인 화면이 꺼져 있지만, 시작할 때 연결한 이벤트는 계속 받음
// 팝업은 게임 중에도 꺼지지 않는 캔버스(SharedCanvas)에 둠. 열고 닫는 연출은 팝업의 PopupView가 함
// NYM 네트워크 매니저가 없는 씬(UI 작업용 씬 등)에서는 아무것도 하지 않음
public class MatchEndedNotice : MonoBehaviour
{
    [SerializeField] private PopupView popup;
    [SerializeField] private Button confirmButton;

    private LexioNetworkManager networkManager;

    private void Start()
    {
        if (popup != null && confirmButton != null)
        {
            confirmButton.onClick.AddListener(popup.Close);
        }
        networkManager = NetworkManager.singleton as LexioNetworkManager;
        if (networkManager != null)
        {
            networkManager.QueueResultReceived += OnQueueResult;
        }
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.QueueResultReceived -= OnQueueResult;
        }
    }

    // requestId 0은 서버가 먼저 보낸 통보
    private void OnQueueResult(RequestResult result)
    {
        if (result.requestId == 0 && result.reason == RejectReason.MatchEnded && popup != null)
        {
            popup.Open();
        }
    }
}
