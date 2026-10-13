using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SignUpController : MonoBehaviour
{
    [SerializeField] private InputField Name_input;
    [SerializeField] private InputField ID_input;
    [SerializeField] private InputField PW_input;
    [SerializeField] private Text LogText;
    [SerializeField] private Button SignUp_btn;

    private void Start()
    {
        SignUpText_viewing(string.Empty);
        SignUp_btn.onClick.AddListener(SignUp_Event);
    }

    private void SignUpText_viewing(string text)
    {
        LogText.text = text;
    }
    public void SignUp_Event()
    {
        if (Name_input.text.Equals(string.Empty) || ID_input.text.Equals(string.Empty) || PW_input.text.Equals(string.Empty))
        {
            SignUpText_viewing("이름/아이디/비밀번호를 입력하세요");
            return;
        }

        if (DBManager.instance.SignUp(Name_input.text, ID_input.text, PW_input.text))
        {   
            //회원가입에 성공하였다
            SignUpText_viewing("회원가입이 완료되었습니다");
        }
        else
        {
            //회원가입 실패
            if (DBManager.instance.duplicationID)
            {
                SignUpText_viewing("아이디가 중복됩니다");
            }
            else if (!DBManager.instance.selectImage)
            {
                SignUpText_viewing("이미지를 선택하세요");
            }
            else
            {
                SignUpText_viewing("닉네임이 중복됩니다");
            }
        }

    }
}
