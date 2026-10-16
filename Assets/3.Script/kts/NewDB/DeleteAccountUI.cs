using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class DeleteAccountUI : MonoBehaviour
{
    [Header("입력창")]
    [SerializeField] private InputField deleteAccountUserID;
    [SerializeField] private InputField deleteAccountUserPwd;
    [SerializeField] private InputField deleteAccountUserPwdConfirm;

    [Header("결과 표시 텍스트")]
    [SerializeField] private Text deleteAccountResultText;


    private LoginNetworkBehaviour deleteAccountBehaviour;
    private void Start()
    {
        deleteAccountResultText.text = string.Empty;
    }

    // 회원가입 버튼에 연결
    public void OnClickDeleteAccount()
    {
        SendDeleteAccountRequest();
    }

    private bool PrepareRequest()
    {
        if (!NetworkClient.isConnected)
        {
            deleteAccountResultText.text = "서버에 연결되지 않았습니다.";
            return false;
        }

        // 내 플레이어 오브젝트를 가져옵니다.
        if (NetworkClient.localPlayer == null)
        {
            deleteAccountResultText.text = "플레이어 오브젝트 생성되지 않았습니다.";
            return false;
        }

        deleteAccountBehaviour = NetworkClient.localPlayer.GetComponent<LoginNetworkBehaviour>();

        if (deleteAccountBehaviour == null)
        {
            deleteAccountResultText.text = "LoginNetworkBehaviour를 찾을 수 없습니다.";
            return false;
        }

        if (deleteAccountUserID.text.Equals(string.Empty) || deleteAccountUserPwd.text.Equals(string.Empty) || deleteAccountUserPwdConfirm.text.Equals(string.Empty))
        {
            deleteAccountResultText.text = "아이디/비밀번호/비밀번호 확인을 입력하세요";
            return false;
        }

        deleteAccountBehaviour.deleteAccountOnAuthResult -= DeleteAccountHandleAuthResult;
        deleteAccountBehaviour.deleteAccountOnAuthResult += DeleteAccountHandleAuthResult;

        return true;
    }

    // 회원가입 요청
    private void SendDeleteAccountRequest()
    {
        if (!PrepareRequest()) return;

        if (!(deleteAccountUserPwd.text == deleteAccountUserPwdConfirm.text))
        {
            deleteAccountResultText.text = "새 비밀번호와 일치하지 않습니다";
            return;
        }

        deleteAccountBehaviour.CmdDeleteAccount(deleteAccountUserID.text.Trim(), deleteAccountUserPwd.text);
    }
    private void DeleteAccountHandleAuthResult(bool success, string message)
    {
        deleteAccountResultText.text = message;

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
        if (deleteAccountBehaviour != null)
        {
            deleteAccountBehaviour.deleteAccountOnAuthResult -= DeleteAccountHandleAuthResult;
        }
    }
}
