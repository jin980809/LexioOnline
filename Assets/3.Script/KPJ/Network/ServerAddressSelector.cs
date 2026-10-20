using Mirror;
using UnityEngine;

// 클라이언트가 접속할 서버 주소를 정함
// 빌드는 항상 serverAddress로 접속하고, 에디터에서는 체크하면 내 PC에 띄운 서버(localAddress)로 접속
// 포트는 Transport(KcpTransport)의 port를 그대로 씀
[RequireComponent(typeof(NetworkManager))]
public class ServerAddressSelector : MonoBehaviour
{
    [Header("서버 주소")]
    [Tooltip("빌드한 클라이언트가 접속할 서버 주소 (AWS 공인 IP)")]
    [SerializeField] private string serverAddress = "52.65.15.177";
    [Tooltip("체크하면 에디터에서만 아래 로컬 주소로 접속 (빌드에는 영향 없음)")]
    [SerializeField] private bool useLocalInEditor;
    [Tooltip("에디터 로컬 테스트용 주소")]
    [SerializeField] private string localAddress = "127.0.0.1";

    private void Awake()
    {
        string address = serverAddress;
        if (Application.isEditor && useLocalInEditor)
        {
            address = localAddress;
        }
        GetComponent<NetworkManager>().networkAddress = address;
    }
}
