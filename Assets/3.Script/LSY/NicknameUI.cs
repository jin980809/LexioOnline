using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class NicknameUI : MonoBehaviour
{
    [Header("활성화 전 UI")]
    [SerializeField] private GameObject showGroup;
    [SerializeField] private Text nickname;
    [SerializeField] private Button editButton;

    [Header("활성화 후 UI")]
    [SerializeField] private GameObject popGroup;
    [SerializeField] private InputField newInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Text errorText;

    private void Awake()
    {
        editButton.onClick.AddListener(OpenEdit);
        saveButton.onClick.AddListener(Save);
        cancelButton.onClick.AddListener(CloseEdit);
    }

    private void OpenEdit()
    {
        newInput.text = "";
        Text placeholder = newInput.placeholder as Text;
        if (placeholder != null)
        {
            placeholder.text = nickname.text;
        }
        SetError("");
        showGroup.SetActive(false);
        popGroup.SetActive(true);
        newInput.ActivateInputField();
    }
    private void CloseEdit()
    {
        showGroup.SetActive(true);
        popGroup.SetActive(false);
    }

    private void Save()
    {
        string newName = newInput.text.Trim();
        if (newName.Length == 0)
        {
            SetError("닉네임을 입력해주세요.");
            return;
        }
        if (newName == nickname.text)
        {
            CloseEdit();
            return;
        }

        //!!!!!!!!!!!!!!!!!!!!!! 여기에 서버변경 요청 및 중복검사 관련 코드 추가
        //닉네임 변경 성공하면 아래 두 줄 실행하고 실패하면 SetError 통해서 사유 표시하게 하기
        nickname.text = newName;
        CloseEdit();
    }

    private void SetError(string message)
    {
        if (errorText != null)
        { 
            errorText.text = message;
            errorText.color = Color.red;
        }
    }
}
