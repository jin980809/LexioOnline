using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeleteAccountController : MonoBehaviour
{
    [SerializeField] private InputField ID_input;
    [SerializeField] private InputField PW_input;
    [SerializeField] private Text LogText;
    [SerializeField] private Button DeleteAccount_btn;

    private void Start()
    {
        DeleteAccountText_viewing(string.Empty);
        DeleteAccount_btn.onClick.AddListener(DeleteAccount_Event);
    }

    private void DeleteAccountText_viewing(string text)
    {
        LogText.text = text;
    }
    public void DeleteAccount_Event()
    {
        if (ID_input.text.Equals(string.Empty) || PW_input.text.Equals(string.Empty))
        {
            DeleteAccountText_viewing("아이디/비밀번호를 입력하세요");
            return;
        }

        if (DBManager.instance.DeleteAccount(ID_input.text, PW_input.text))
        {
            //회원탈퇴에 성공하였다
            DeleteAccountText_viewing("회원탈퇴가 완료되었습니다");
        }
        else
        {
            //회원탈퇴 실패
            DeleteAccountText_viewing("아이디/비밀번호가 일치하지 않습니다");
        }

    }
}

