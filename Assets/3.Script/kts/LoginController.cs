using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoginController : MonoBehaviour
{
    [SerializeField] private InputField ID_input;
    [SerializeField] private InputField PW_input;
    [SerializeField] private Text LogText;
    [SerializeField] private Button Login_btn;

    private void Start()
    {
        LogText_viewing(string.Empty);
        Login_btn.onClick.AddListener(Login_Event);
    }

    private void LogText_viewing(string text)
    {
        LogText.text = text;
    }
    public void Login_Event()
    {
        if (ID_input.text.Equals(string.Empty) || PW_input.text.Equals(string.Empty))
        {
            LogText_viewing("아이디/비밀번호를 입력하세요");
            return;
        }

        if (DBManager.instance.Login(ID_input.text, PW_input.text))
        {
            //로그인이 성공되었다
            UserInfo info = DBManager.instance.info;
            Debug.Log($"{info.userName} | {info.userID} | {info.imageNum} | {info.first} | {info.second} | {info.third} | {info.fourth} | {info.playNum} | {info.averageRank}");
            gameObject.SetActive(false);
        }
        else
        {
            //로그인이 실패
            LogText_viewing("아이디/비밀번호를 확인하세요");
        }
    }
}
