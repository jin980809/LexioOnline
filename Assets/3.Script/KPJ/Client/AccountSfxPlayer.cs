using Mirror;
using UnityEngine;

// 로그인·회원가입·비밀번호 변경 등 계정 요청이 실패하면 에러 효과음을 냄 (SoundManager와 같은 오브젝트에 붙임)
// 서버 응답(NYMAccountAuthenticator의 결과 이벤트)만 보고 판단하므로, 서버에 보내기 전에 걸러지는 입력 오류에는 소리가 없음
// 인증기가 없는 씬(테스트용 접속 씬 등)에서는 아무것도 하지 않음
[RequireComponent(typeof(SoundManager))]
public class AccountSfxPlayer : MonoBehaviour
{
    private SoundManager soundManager;
    private NYMAccountAuthenticator authenticator;

    private void Awake()
    {
        soundManager = GetComponent<SoundManager>();
    }

    private void Start()
    {
        if (NetworkManager.singleton != null)
        {
            authenticator = NetworkManager.singleton.authenticator as NYMAccountAuthenticator;
        }
        if (authenticator != null)
        {
            authenticator.ResultReceived += OnResultReceived;
            authenticator.ActionResultReceived += OnActionResultReceived;
        }
    }

    private void OnDestroy()
    {
        if (authenticator != null)
        {
            authenticator.ResultReceived -= OnResultReceived;
            authenticator.ActionResultReceived -= OnActionResultReceived;
        }
    }

    // 로그인 결과
    private void OnResultReceived(AccountResultMessage result)
    {
        PlayIfFailed(result.code);
    }

    // 아이디 중복 확인·회원가입·비밀번호 변경·탈퇴·프로필 수정 결과
    private void OnActionResultReceived(AccountActionResult result)
    {
        PlayIfFailed(result.code);
    }

    private void PlayIfFailed(AccountResultCode code)
    {
        if (code != AccountResultCode.Success)
        {
            soundManager.PlaySfx(SoundManager.Sfx.Error);
        }
    }
}
