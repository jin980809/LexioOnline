using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class NewPasswordUI : MonoBehaviour
{
    [Header("입력창")]
    [SerializeField] private InputField newPwdUserID;
    [SerializeField] private InputField newPwdUserPwd;
    [SerializeField] private InputField newPwdNewUserPwd;
    [SerializeField] private InputField newPwdNewUserPwdConfirm;

    [Header("결과 표시 텍스트")]
    [SerializeField] private Text newPwdResultText;


    private LoginNetworkBehaviour newPwdBehaviour;
    private void Start()
    {
        newPwdResultText.text = string.Empty;
    }

    // 회원가입 버튼에 연결
    public void OnClickNewPwd()
    {
        SendNewPwdRequest();
    }

    private bool PrepareRequest()
    {
        if (!NetworkClient.isConnected)
        {
            newPwdResultText.text = "서버에 연결되지 않았습니다.";
            return false;
        }

        // 내 플레이어 오브젝트를 가져옵니다.
        if (NetworkClient.localPlayer == null)
        {
            newPwdResultText.text = "플레이어 오브젝트 생성되지 않았습니다.";
            return false;
        }

        newPwdBehaviour = NetworkClient.localPlayer.GetComponent<LoginNetworkBehaviour>();

        if (newPwdBehaviour == null)
        {
            newPwdResultText.text = "LoginNetworkBehaviour를 찾을 수 없습니다.";
            return false;
        }

        if (newPwdUserID.text.Equals(string.Empty) || newPwdUserPwd.text.Equals(string.Empty) || newPwdNewUserPwd.text.Equals(string.Empty))
        {
            newPwdResultText.text = "아이디/비밀번호/새 비밀번호를 입력하세요";
            return false;
        }

        newPwdBehaviour.newPwdOnAuthResult -= NewPwdHandleAuthResult;
        newPwdBehaviour.newPwdOnAuthResult += NewPwdHandleAuthResult;

        return true;
    }

    // 회원가입 요청
    private void SendNewPwdRequest()
    {
        if (!PrepareRequest()) return;

        if (!(newPwdNewUserPwd.text == newPwdNewUserPwdConfirm.text))
        {
            newPwdResultText.text = "새 비밀번호와 일치하지 않습니다";
            return;
        }

        newPwdBehaviour.CmdNewPassword(newPwdUserID.text.Trim(), newPwdUserPwd.text, newPwdNewUserPwd.text);
    }
    private void NewPwdHandleAuthResult(bool success, string message)
    {
        newPwdResultText.text = message;

        if (success)
        {
            Debug.Log("요청 성공");
        }
        else
        {
            Debug.LogWarning("요청 실패");
        }
    }

    private void OnDestroy()
    {
        if (newPwdBehaviour != null)
        {
            newPwdBehaviour.newPwdOnAuthResult -= NewPwdHandleAuthResult;
        }
    }
}
