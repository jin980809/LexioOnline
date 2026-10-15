using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class LoginUI : MonoBehaviour
{
    [Header("입력창")]
    [SerializeField] private InputField loginUserID;
    [SerializeField] private InputField loginUserPwd;

    [SerializeField] private InputField registerUserName;
    [SerializeField] private InputField registerUserID;
    [SerializeField] private InputField registerUserPwd;

    [Header("결과 표시")]
    [SerializeField] private Text loginResultText;
    [SerializeField] private Text registerResultText;

    private LoginNetworkBehaviour loginBehaviour;

    // 로그인 버튼에 연결
    public void OnClickLogin()
    {
        SendLoginRequest();
    }

    // 회원가입 버튼에 연결
    //public void OnClickRegister()
    //{
    //    SendRegisterRequest();
    //}

    // 로그인/회원가입 요청 전 공통 확인
    private bool PrepareRequest()
    {
        if (!NetworkClient.isConnected)
        {
            Debug.Log("서버에 연결되지 않았습니다.");
            return false;
        }

        // 내 플레이어 오브젝트를 가져옵니다.
        if (NetworkClient.localPlayer == null)
        {
            Debug.Log("플레이어 오브젝트 생성되지 않았습니다.");
            return false;
        }

        loginBehaviour = NetworkClient.localPlayer.GetComponent<LoginNetworkBehaviour>();

        if (loginBehaviour == null)
        {
            Debug.Log("LoginNetworkBehaviour를 찾을 수 없습니다.");
            return false;
        }

        //string username = userID.text;
        //string password = userPwd.text;
        //
        //if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        //{
        //    resultText.text = "아이디와 비밀번호를 입력해 주세요.";
        //    return false;
        //}

        // 결과 이벤트가 중복 등록되지 않도록 합니다.
        loginBehaviour.OnAuthResult -= HandleAuthResult;
        loginBehaviour.OnAuthResult += HandleAuthResult;

        return true;
    }

    // 로그인 요청
    private void SendLoginRequest()
    {
        if (!PrepareRequest()) return;

        loginResultText.text = "로그인 확인 중...";

        loginBehaviour.CmdLogin(loginUserID.text.Trim(), loginUserID.text);
    }

    // 회원가입 요청
    private void SendRegisterRequest()
    {
        if (!PrepareRequest())
            return;

        registerResultText.text = "회원가입 처리 중...";
    
        loginBehaviour.CmdRegister(registerUserName.text.Trim(), registerUserName.text);
    }

    // 서버에서 결과를 반환하면 실행
    private void HandleAuthResult(bool success, string message)
    {
        registerResultText.text = message;

        if (success)
        {
            //여기다 창 닫기와 userinfo에 데이터 넣기
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
            loginBehaviour.OnAuthResult -= HandleAuthResult;
        }
    }
}