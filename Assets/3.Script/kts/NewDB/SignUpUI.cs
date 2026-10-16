using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class SignUpUI : MonoBehaviour
{
    [Header("입력창")]
    [SerializeField] private InputField signUpUserName;
    [SerializeField] private InputField signUpUserID;
    [SerializeField] private InputField signUpUserPwd;
    [SerializeField] private InputField signUpUserPwdConfirm;

    [Header("결과 표시 텍스트")]
    [SerializeField] private Text signUpResultText;

    [Header("이미지 버튼")]
    [SerializeField] private Button[] profileImage;
    [SerializeField] private int imageNum;

    [Header("이미지")]
    [SerializeField] private Image profileCheckImage;
    [SerializeField] private Sprite[] profileImageArray;


    private LoginNetworkBehaviour signUpBehaviour;
    private void Start()
    {
        signUpResultText.text = string.Empty;
        for (int i = 0; i < profileImage.Length; i++)
        {
            int index = i; // 지역 변수로 현재 인덱스 캡처
            profileImage[index].onClick.AddListener(() => TaskOnClick(index));
        }
    }
    
    // 회원가입 버튼에 연결
    public void OnClickSignUp()
    {
        SendSignUpRequest();
    }

    private bool PrepareRequest()
    {
        if (!NetworkClient.isConnected)
        {
            signUpResultText.text = "서버에 연결되지 않았습니다.";
            return false;
        }

        // 내 플레이어 오브젝트를 가져옵니다.
        if (NetworkClient.localPlayer == null)
        {
            signUpResultText.text = "플레이어 오브젝트 생성되지 않았습니다.";
            return false;
        }

        signUpBehaviour = NetworkClient.localPlayer.GetComponent<LoginNetworkBehaviour>();

        if (signUpBehaviour == null)
        {
            signUpResultText.text = "LoginNetworkBehaviour를 찾을 수 없습니다.";
            return false;
        }

        if (signUpUserID.text.Equals(string.Empty) || signUpUserPwd.text.Equals(string.Empty) || signUpUserName.text.Equals(string.Empty))
        {
            signUpResultText.text = "아이디/비밀번호/닉네임을 입력하세요";
            return false;
        }

        signUpBehaviour.signUpOnAuthResult -= SignUpHandleAuthResult;
        signUpBehaviour.signUpOnAuthResult += SignUpHandleAuthResult;

        return true;
    }

    // 회원가입 요청
    private void SendSignUpRequest()
    {
        if (!PrepareRequest()) return;

        if (!(signUpUserPwd.text == signUpUserPwdConfirm.text))
        {
            signUpResultText.text = "비밀번호가 다릅니다";
            return;
        }

        signUpBehaviour.CmdSignUp(signUpUserID.text.Trim(), signUpUserPwd.text, signUpUserName.text, imageNum);
    }
    private void SignUpHandleAuthResult(bool success, string message)
    {
        signUpResultText.text = message;

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
        if (signUpBehaviour != null)
        {
            signUpBehaviour.signUpOnAuthResult -= SignUpHandleAuthResult;
        }
    }

    void TaskOnClick(int buttonIndex)
    {
        imageNum = buttonIndex;
        profileCheckImage.sprite = profileImageArray[buttonIndex];
    }

}
