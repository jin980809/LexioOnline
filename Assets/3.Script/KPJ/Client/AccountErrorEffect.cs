using Mirror;
using UnityEngine;
using UnityEngine.UI;

// 계정 요청이 진행되지 않으면 그 창에 실패 효과(kts의 ErrorAni: 창 흔들림 + 붉은 번쩍임)를 냄
//  - 로그인: 서버가 로그인을 거절했을 때 (NYMAccountAuthenticator의 로그인 결과)
//  - 회원가입·비밀번호 변경·계정 삭제: 그 창에 실패 안내(빨간 글자)가 뜰 때 (NYMAccountUI의 ErrorFeedbackShown)
//    입력 오류(비밀번호 불일치 등)와 서버 거절(중복 아이디 등) 모두. 안내 글자가 들어 있는 창의 효과를 냄
// 인증기·계정 UI가 없는 씬(UI 작업용 씬 등)에서는 아무것도 하지 않음
public class AccountErrorEffect : MonoBehaviour
{
    [SerializeField] private ErrorAni loginEffect;
    [SerializeField] private ErrorAni signUpEffect;
    [SerializeField] private ErrorAni passwordChangeEffect;
    [SerializeField] private ErrorAni accountDeleteEffect;
    // 각 효과가 흐리게 하는 번쩍임 (ErrorAni의 canvasGroup과 같은 것)
    // 번쩍임이 사라지는 중에 창이 닫히면 그 밝기로 멈춰 있다가 다음에 열 때 보이므로, 창이 꺼져 있으면 투명하게 되돌림
    [SerializeField] private CanvasGroup[] flashes;

    private NYMAccountAuthenticator authenticator;
    private NYMAccountUI accountUi;

    private void Start()
    {
        if (NetworkManager.singleton != null)
        {
            authenticator = NetworkManager.singleton.authenticator as NYMAccountAuthenticator;
        }
        if (authenticator != null)
        {
            authenticator.ResultReceived += OnLoginResult;
        }

        accountUi = FindAnyObjectByType<NYMAccountUI>(FindObjectsInactive.Include);
        if (accountUi != null)
        {
            accountUi.ErrorFeedbackShown += OnErrorFeedbackShown;
        }
    }

    private void OnDestroy()
    {
        if (authenticator != null)
        {
            authenticator.ResultReceived -= OnLoginResult;
        }
        if (accountUi != null)
        {
            accountUi.ErrorFeedbackShown -= OnErrorFeedbackShown;
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

    private void OnLoginResult(AccountResultMessage result)
    {
        if (result.code != AccountResultCode.Success)
        {
            Play(loginEffect);
        }
    }

    // 안내 글자가 들어 있는 창의 효과를 냄
    private void OnErrorFeedbackShown(Text label)
    {
        if (label != null)
        {
            if (IsInside(label, signUpEffect))
            {
                Play(signUpEffect);
            }
            else if (IsInside(label, passwordChangeEffect))
            {
                Play(passwordChangeEffect);
            }
            else if (IsInside(label, accountDeleteEffect))
            {
                Play(accountDeleteEffect);
            }
        }
    }

    private bool IsInside(Text label, ErrorAni effect)
    {
        bool result = false;
        if (effect != null)
        {
            result = label.transform.IsChildOf(effect.transform);
        }
        return result;
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
