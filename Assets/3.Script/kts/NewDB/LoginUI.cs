using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class LoginUI : MonoBehaviour
{
    [Header("입력창")]
    [SerializeField] private InputField loginUserID;
    [SerializeField] private InputField loginUserPwd;

    [Header("결과 표시 텍스트")]
    [SerializeField] private Text loginResultText;

    private LoginNetworkBehaviour loginBehaviour;
    private void Start()
    {
        loginResultText.text = string.Empty;
    }
    // 로그인 버튼에 연결
    public void OnClickLogin()
    {
        SendLoginRequest();
    }

    // 로그인/회원가입 요청 전 공통 확인
    private bool PrepareRequest()
    {
        if (!NetworkClient.isConnected)
        {
            loginResultText.text = "서버에 연결되지 않았습니다.";
            return false;
        }

        // 내 플레이어 오브젝트를 가져옵니다.
        if (NetworkClient.localPlayer == null)
        {
            loginResultText.text = "플레이어 오브젝트 생성되지 않았습니다.";
            return false;
        }

        loginBehaviour = NetworkClient.localPlayer.GetComponent<LoginNetworkBehaviour>();

        if (loginBehaviour == null)
        {
            loginResultText.text = "LoginNetworkBehaviour를 찾을 수 없습니다.";
            return false;
        }

        if (loginUserID.text.Equals(string.Empty) || loginUserPwd.text.Equals(string.Empty))
        {
            loginResultText.text = "아이디/비밀번호를 입력하세요";
            return false;
        }

        loginBehaviour.loginOnAuthResult -= LoginHandleAuthResult;
        loginBehaviour.loginOnAuthResult += LoginHandleAuthResult;

        return true;
    }

    // 로그인 요청
    private void SendLoginRequest()
    {
        if (!PrepareRequest()) return;

        loginBehaviour.CmdLogin(loginUserID.text.Trim(), loginUserPwd.text);
    }

    // 서버에서 결과를 반환하면 실행
    private void LoginHandleAuthResult(bool success, string message, UserInfo userinfo)
    {
        loginResultText.text = message;

        if (success)
        {
            PlayerDBManager.instance.UserInfoUpDate(userinfo);
            Debug.Log("요청 성공");
        }
        else
        {
            Debug.LogWarning("요청 실패");
        }
    }

    private void OnDestroy()
    {
        if (loginBehaviour != null)
        {
            loginBehaviour.loginOnAuthResult -= LoginHandleAuthResult;
        }
    }
}