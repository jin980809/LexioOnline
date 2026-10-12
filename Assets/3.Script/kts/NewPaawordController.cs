using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NewPaaswordController : MonoBehaviour
{
    [SerializeField] private InputField ID_input;
    [SerializeField] private InputField PW_input;
    [SerializeField] private InputField New_PW_input;
    [SerializeField] private Text LogText;
    [SerializeField] private Button NewPassword_btn;

    private void Start()
    {
        NewPasswordText_viewing(string.Empty);
        NewPassword_btn.onClick.AddListener(NewPassword_Event);
    }

    private void NewPasswordText_viewing(string text)
    {
        LogText.text = text;
    }
    public void NewPassword_Event()
    {
        if (ID_input.text.Equals(string.Empty) || PW_input.text.Equals(string.Empty) || New_PW_input.text.Equals(string.Empty))
        {
            NewPasswordText_viewing("아이디/비밀번호/새비밀번호를 입력하세요");
            return;
        }

        if (DBManager.instance.NewPassword(ID_input.text, PW_input.text, New_PW_input.text))
        {//비밀번호 변경에 성공
            NewPasswordText_viewing("비밀번호가 변경되었습니다");
        }
        else
        {
            //비번변경 실패
            NewPasswordText_viewing("아이디/비밀번호가 일치하지 않습니다");
        }

    }
}

