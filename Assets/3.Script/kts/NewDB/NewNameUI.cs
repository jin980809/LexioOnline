using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class NewNameUI : MonoBehaviour
{
    [Header("입력창")]
    [SerializeField] private InputField newUserName;

    [Header("현 닉네임 텍스트")]
    [SerializeField] private Text currentName;

    [Header("결과 표시 텍스트")]
    [SerializeField] private Text newNameResultText;


    private LoginNetworkBehaviour newNameBehaviour;
    private void Start()
    {
        newNameResultText.text = string.Empty;
        currentName.text = string.Empty;
    }
    private void OnEnable()
    {
        if(!(PlayerDBManager.instance.userInfo == null))
        {
            currentName.text = PlayerDBManager.instance.userInfo.userName;
        }
    }

    // 회원가입 버튼에 연결
    public void OnClickNewName()
    {
        SendNewNameRequest();
    }

    private bool PrepareRequest()
    {
        if (!NetworkClient.isConnected)
        {
            newNameResultText.text = "서버에 연결되지 않았습니다.";
            return false;
        }

        // 내 플레이어 오브젝트를 가져옵니다.
        if (NetworkClient.localPlayer == null)
        {
            newNameResultText.text = "플레이어 오브젝트 생성되지 않았습니다.";
            return false;
        }

        newNameBehaviour = NetworkClient.localPlayer.GetComponent<LoginNetworkBehaviour>();

        if (newNameBehaviour == null)
        {
            newNameResultText.text = "LoginNetworkBehaviour를 찾을 수 없습니다.";
            return false;
        }

        if (newUserName.text.Equals(string.Empty))
        {
            newNameResultText.text = "새 닉네임을 입력하세요";
            return false;
        }

        newNameBehaviour.newNameOnAuthResult -= NewNameHandleAuthResult;
        newNameBehaviour.newNameOnAuthResult += NewNameHandleAuthResult;

        return true;
    }

    // 회원가입 요청
    private void SendNewNameRequest()
    {
        if (!PrepareRequest()) return;

        if (PlayerDBManager.instance.userInfo == null)
        {
            newNameResultText.text = "로그인 정보가 없습니다";
            return;
        }

        newNameBehaviour.CmdNewName(PlayerDBManager.instance.userInfo.userID, newUserName.text);
    }
    private void NewNameHandleAuthResult(bool success, string message, UserInfo userinfo)
    {
        newNameResultText.text = message;

        if (success)
        {
            PlayerDBManager.instance.UserInfoUpDate(userinfo);
            Debug.Log("요청 성공");

            if (!(PlayerDBManager.instance.userInfo == null))
            {
                currentName.text = PlayerDBManager.instance.userInfo.userName;
            }
        }
        else
        {
            Debug.LogWarning("요청 실패");
        }
    }

    private void OnDestroy()
    {
        if (newNameBehaviour != null)
        {
            newNameBehaviour.newNameOnAuthResult -= NewNameHandleAuthResult;
        }
    }
}
