using System;
using Mirror;
using UnityEngine;

public class LoginNetworkBehaviour : NetworkBehaviour
{
    // 서버에서 로그인 성공 여부 관리
    //[SyncVar]
    //private bool isLoggedIn = false;

    // 서버에서 DB에 접근하는 클래스
    private ServerDBManager serverDBManager;
    private UserInfo loggedInUser;

    // 클라이언트 UI에 인증 결과를 전달하는 이벤트
    public event Action<bool, string> OnAuthResult;

    public override void OnStartServer()
    {
        serverDBManager = FindFirstObjectByType<ServerDBManager>();

        if (serverDBManager == null)
        {
            Debug.LogError("ServerDatabase를 찾을 수 없습니다.");
        }
    }

    // 클라이언트 -> 서버 로그인 요청
    [Command]
    public void CmdLogin(string _id, string _pw)
    {
        // 입력값 검증
        if (string.IsNullOrWhiteSpace(_id) ||string.IsNullOrEmpty(_pw) || _id.Length > 32 || _id.Length > 128) 
        {
            TargetAuthResult(connectionToClient,false,"아이디 또는 비밀번호를 확인해 주세요.");
            return;
        }

        if (serverDBManager == null)
        {
            Debug.LogError("serverDBManager가 null입니다!");

            TargetAuthResult(connectionToClient, false,"서버 DB가 준비되지 않았습니다.");
            return;
        }
        UserInfo userInfo;
        // 실제 DB 접근은 서버에서만 실행
        bool success = serverDBManager.Login(_id, _pw, out userInfo);

        if (!success || userInfo == null)
        {
            TargetAuthResult(connectionToClient, false,"아이디 또는 비밀번호가 올바르지 않습니다.");
            return;
        }

        // 로그인한 사용자 정보 저장
        loggedInUser = userInfo;

        TargetAuthResult(connectionToClient, true, "로그인 성공");
    }

    //클라이언트 -> 서버 회원가입 요청
    [Command]
    public void CmdRegister(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) ||string.IsNullOrEmpty(password) ||username.Length > 32 ||password.Length > 128)
        {
            TargetAuthResult(connectionToClient,false,"입력값을 확인해 주세요.");
            return;
        }
    
        // 회원가입 메서드는 기존 DB 클래스에 구현해야 합니다.
        bool success = serverDatabase.Register(username, password);
    
        TargetAuthResult(connectionToClient,success,success ? "회원가입 성공" : "회원가입 실패");
    }

    // 서버 -> 요청한 클라이언트 결과 반환
    [TargetRpc]
    private void TargetAuthResult(NetworkConnectionToClient target, bool success,string message)
    {
        // 이 메서드는 클라이언트에서 실행됩니다.
        OnAuthResult?.Invoke(success, message);
        Debug.Log(message);
    }

    [Server]
    public bool IsLoggedIn()
    {
        return loggedInUser != null;
    }
}
