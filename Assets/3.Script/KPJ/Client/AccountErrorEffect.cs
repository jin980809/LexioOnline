using Mirror;
using UnityEngine;

// 계정 요청이 실패하면 그 요청을 보낸 창에 실패 효과(kts의 ErrorAni: 창 흔들림 + 붉은 번쩍임)를 냄
//  - 로그인 실패: 로그인 창
//  - 아이디 중복 확인·회원가입 실패: 회원가입 창
//  - 비밀번호 변경 실패: 비밀번호 변경 창
// 서버 응답(NYMAccountAuthenticator의 결과 이벤트)만 보고 판단하므로, 서버에 보내기 전에 걸러지는 입력 오류에는 효과가 없음 (AccountSfxPlayer와 같은 기준)
// 인증기가 없는 씬(UI 작업용 씬 등)에서는 아무것도 하지 않음
public class AccountErrorEffect : MonoBehaviour
{
    [SerializeField] private ErrorAni loginEffect;
    [SerializeField] private ErrorAni signUpEffect;
    [SerializeField] private ErrorAni passwordChangeEffect;
    // 각 효과가 흐리게 하는 번쩍임 (ErrorAni의 canvasGroup과 같은 것)
    // 번쩍임이 사라지는 중에 창이 닫히면 그 밝기로 멈춰 있다가 다음에 열 때 보이므로, 창이 꺼져 있으면 투명하게 되돌림
    [SerializeField] private CanvasGroup[] flashes;

    private NYMAccountAuthenticator authenticator;

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

    private void Update()
    {
        for (int i = 0; i < flashes.Length; i++)
        {
            if (flashes[i] != null && flashes[i].alpha > 0f && flashes[i].gameObject.activeInHierarchy == false)
            {
                flashes[i].alpha = 0f;
            }
        }
    }

    // 로그인 결과
    private void OnResultReceived(AccountResultMessage result)
    {
        if (result.code != AccountResultCode.Success)
        {
            Play(loginEffect);
        }
    }

    // 아이디 중복 확인·회원가입·비밀번호 변경 등 결과
    private void OnActionResultReceived(AccountActionResult result)
    {
        if (result.code != AccountResultCode.Success)
        {
            if (result.operation == AccountOperation.CheckId || result.operation == AccountOperation.Register)
            {
                Play(signUpEffect);
            }
            else if (result.operation == AccountOperation.ChangePassword)
            {
                Play(passwordChangeEffect);
            }
        }
    }

    // 창이 꺼져 있으면 효과를 낼 수 없으므로(코루틴 시작 불가) 보이는 창에만 냄
    private void Play(ErrorAni effect)
    {
        if (effect != null && effect.isActiveAndEnabled)
        {
            effect.ErrorAniStart();
        }
    }
}
