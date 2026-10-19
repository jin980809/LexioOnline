using System;
using Mirror;
using UnityEngine;

public class LoginNetworkBehaviour : NetworkBehaviour
{
    // 서버에서 DB에 접근하는 클래스
    private ServerDBManager serverDBManager;
    public UserInfo loggedInUser { get; private set; }

    // 클라이언트 UI에 인증 결과를 전달하는 이벤트
    public event Action<bool, string, UserInfo> loginOnAuthResult;
    public event Action<bool, string> signUpOnAuthResult;
    public event Action<bool, string> newPwdOnAuthResult;
    public event Action<bool, string> deleteAccountOnAuthResult;
    public event Action<bool, string, UserInfo> newNameOnAuthResult;

    public override void OnStartServer()
    {
        serverDBManager = FindFirstObjectByType<ServerDBManager>();

        if (serverDBManager == null)
        {
            Debug.LogError("ServerDatabase를 찾을 수 없습니다.");
        }
    }

    //---------------------------------------------------------------------클라이언트 요청------------------------------------------------------------------------

    // 클라이언트 -> 서버 로그인 요청
    [Command]
    public void CmdLogin(string _id, string _pw)
    {
        if (serverDBManager == null)
        {
            Debug.LogError("serverDBManager가 null입니다!");

            LoginTargetAuthResult(connectionToClient, false,"서버 DB가 준비되지 않았습니다.", null);
            return;
        }

        UserInfo userInfo;
        
        // 실제 DB 접근은 서버에서만 실행
        bool success = serverDBManager.Login(_id, _pw, out userInfo);
        
        // 정보전달 확인용(추후 제거)
        Debug.Log($"[Server] DB 로그인 결과: {success}, " +$"UserInfo null 여부: {userInfo == null}");

        if (!success || userInfo == null)
        {
            LoginTargetAuthResult(connectionToClient, false, "아이디 또는 비밀번호가 올바르지 않습니다.", null);
            return;
        }

        // 정보전달 확인용(추후 제거)
        Debug.Log($"[Server] RPC 전송 직전: " +$"ID={userInfo.userID}, Name={userInfo.userName}");

        LoginTargetAuthResult(connectionToClient, true, "로그인 성공", userInfo);
    }

    //클라이언트 -> 서버 회원가입 요청
    [Command]
    public void CmdSignUp(string _id, string _pw, string _name, int _imageNum)
    {
        // 입력값 검증 (추후 추가 예정)
        //if (string.IsNullOrWhiteSpace(_id) ||string.IsNullOrEmpty(_pw) || _id.Length > 32 || _pw.Length > 128) 
        //{
        //    LoginTargetAuthResult(connectionToClient,false,"입력값을 확인해 주세요.");
        //    return;
        //}

        if (serverDBManager == null)
        {
            Debug.LogError("serverDBManager가 null입니다!");

            SignUpTargetAuthResult(connectionToClient, false, "서버 DB가 준비되지 않았습니다.");
            return;
        }

        bool success = serverDBManager.SignUp(_id, _pw, _name, _imageNum);

        if (serverDBManager.duplicationID)
        {
            SignUpTargetAuthResult(connectionToClient, false, "아이디가 중복됩니다");
            return;
        }
        if (serverDBManager.duplicationName)
        {
            SignUpTargetAuthResult(connectionToClient, false, "닉네임이 중복됩니다");
            return;
        }

        SignUpTargetAuthResult(connectionToClient,success,success ? "회원가입 성공" : "회원가입 실패");
    }

    //클라이언트 -> 서버 비밀번호 변경 요청
    [Command]
    public void CmdNewPassword(string _id, string _pw, string _newPw)
    {
        // 입력값 검증 (추후 추가 예정)
        //if (string.IsNullOrWhiteSpace(_id) ||string.IsNullOrEmpty(_pw) || _id.Length > 32 || _pw.Length > 128) 
        //{
        //    LoginTargetAuthResult(connectionToClient,false,"입력값을 확인해 주세요.");
        //    return;
        //}

        if (serverDBManager == null)
        {
            Debug.LogError("serverDBManager가 null입니다!");

            NewPwdTargetAuthResult(connectionToClient, false, "서버 DB가 준비되지 않았습니다.");
            return;
        }

        bool success = serverDBManager.NewPassword(_id, _pw, _newPw);

        if (!success)
        {
            NewPwdTargetAuthResult(connectionToClient, false, "계정이 확인되지 않습니다");
            return;
        }

        NewPwdTargetAuthResult(connectionToClient, success, success ? "비밀번호 변경 완료" : "비밀번호 변경 실패");
    }

    //클라이언트 -> 서버 계정 삭제 요청
    [Command]
    public void CmdDeleteAccount(string _id, string _pw)
    {
        if (serverDBManager == null)
        {
            Debug.LogError("serverDBManager가 null입니다!");

            DeleteAccountTargetAuthResult(connectionToClient, false, "서버 DB가 준비되지 않았습니다.");
            return;
        }

        bool success = serverDBManager.DeleteAccount(_id, _pw);

        if (!success)
        {
            DeleteAccountTargetAuthResult(connectionToClient, false, "계정이 확인되지 않습니다");
            return;
        }

        DeleteAccountTargetAuthResult(connectionToClient, success, success ? "계정 삭제 완료" : "계정 삭제 실패");
    }

    //클라이언트 -> 서버 닉네임 변경 요청
    [Command]
    public void CmdNewName(string _id, string _newName)
    {
        if (serverDBManager == null)
        {
            Debug.LogError("serverDBManager가 null입니다!");

            NewNameTargetAuthResult(connectionToClient, false, "서버 DB가 준비되지 않았습니다.", null);
            return;
        }

        UserInfo userInfo;

        bool success = serverDBManager.NewName(_id, _newName, out userInfo);

        if (!success || userInfo == null)
        {
            NewNameTargetAuthResult(connectionToClient, false, "계정이 확인되지 않습니다", null);
            return;
        }

        NewNameTargetAuthResult(connectionToClient, success, success ? "닉네임 변경 완료" : "닉네임 변경 실패", userInfo);
    }
    //----------------------------------------------------------------------클라이언트 반환--------------------------------------------------------------------------------

    // 서버 -> 요청한 로그인 결과 클라이언트 반환
    [TargetRpc]
    private void LoginTargetAuthResult(NetworkConnectionToClient target, bool success, string message, UserInfo userinfo)
    {
        // 이 메서드는 클라이언트에서 실행됩니다.

        // 정보전달 확인용(추후 제거)
        Debug.Log($"[Client] TargetRpc 수신: success={success}, " + $"UserInfo null 여부={userinfo == null}");

        loginOnAuthResult?.Invoke(success, message, userinfo);
        Debug.Log(message);

    }

    [TargetRpc]
    private void SignUpTargetAuthResult(NetworkConnectionToClient target, bool success, string message)
    {
        // 이 메서드는 클라이언트에서 실행됩니다.
        signUpOnAuthResult?.Invoke(success, message);
        Debug.Log(message);
    }

    [TargetRpc]
    private void NewPwdTargetAuthResult(NetworkConnectionToClient target, bool success, string message)
    {
        // 이 메서드는 클라이언트에서 실행됩니다.
        newPwdOnAuthResult?.Invoke(success, message);
        Debug.Log(message);
    }

    [TargetRpc]
    private void DeleteAccountTargetAuthResult(NetworkConnectionToClient target, bool success, string message)
    {
        // 이 메서드는 클라이언트에서 실행됩니다.
        deleteAccountOnAuthResult?.Invoke(success, message);
        Debug.Log(message);
    }

    [TargetRpc]
    private void NewNameTargetAuthResult(NetworkConnectionToClient target, bool success, string message, UserInfo userinfo)
    {
        // 이 메서드는 클라이언트에서 실행됩니다.
        newNameOnAuthResult?.Invoke(success, message, userinfo);
        Debug.Log(message);
    }

    [Server]
    public bool IsLoggedIn()
    {
        return loggedInUser != null;
    }
}
