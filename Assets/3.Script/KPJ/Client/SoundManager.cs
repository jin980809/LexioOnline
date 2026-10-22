using System;
using UnityEngine;

// 배경음 / 효과음 재생과 소리 설정(크기, 음소거, 내 차례 알림음)을 한곳에서 관리
// 설정은 PlayerPrefs에 저장되어 다음 실행에도 유지됨
// 소리를 내야 하는 컴포넌트는 인스펙터에서 이 컴포넌트를 연결해서 씀 (static 접근 없음)
// 크기는 설정창 슬라이더와 같은 0~100 정수. 음소거는 크기와 따로 저장하므로 음소거를 풀면 원래 크기로 돌아감
public class SoundManager : MonoBehaviour
{
    public enum Sfx
    {
        Button,
        Put,
        Pass,
        Receive,
        ChipGather,
        TimerWarning,
        Error,
        OtherTurn,
        Select
    }

    private const string BgmVolumeKey = "Lexio.Sound.BgmVolume";
    private const string SfxVolumeKey = "Lexio.Sound.SfxVolume";
    private const string BgmMutedKey = "Lexio.Sound.BgmMuted";
    private const string SfxMutedKey = "Lexio.Sound.SfxMuted";
    private const string TurnAlertKey = "Lexio.Sound.TurnAlert";
    private const int MaxVolume = 100;

    [Header("Sources (비어 있으면 Awake에서 만듦)")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("BGM")]
    [SerializeField] private AudioClip bgmClip;
    [SerializeField] private bool playBgmOnStart = true;

    [Header("SFX")]
    [SerializeField] private AudioClip buttonClip;
    // 패를 낼 때
    [SerializeField] private AudioClip putClip;
    [SerializeField] private AudioClip passClip;
    // 패를 받을 때 (나눠 주기)
    [SerializeField] private AudioClip receiveClip;
    // 칩을 모을 때 (라운드 정산)
    [SerializeField] private AudioClip chipGatherClip;
    // 내 패 선택 효과음
    [SerializeField] private AudioClip selectClip;
    // 남은 시간 경고
    [SerializeField] private AudioClip timerWarningClip;
    // 로그인이나 회원가입 시 실패 혹은 에러가 뜨면 재생
    [SerializeField] private AudioClip errorClip;
    // 다른 사람의 차례일 때 울리는 알림음
    [SerializeField] private AudioClip otherTurnClip;
    // 내 차례 알림음. 효과음 크기를 따르고, 알림음 설정이 꺼져 있으면 재생하지 않음
    [SerializeField] private AudioClip turnAlertClip;


    [Header("Default (저장된 설정이 없을 때)")]
    [SerializeField, Range(0, MaxVolume)] private int defaultBgmVolume = 70;
    [SerializeField, Range(0, MaxVolume)] private int defaultSfxVolume = 70;

    private int bgmVolume;
    private int sfxVolume;
    private bool isBgmMuted;
    private bool isSfxMuted;
    private bool isTurnAlertOn;

    // 설정이 바뀌면 알림 (설정창 UI 갱신용)
    public event Action SettingsChanged;

    public int BgmVolume
    {
        get { return bgmVolume; }
    }

    public int SfxVolume
    {
        get { return sfxVolume; }
    }

    public bool IsBgmMuted
    {
        get { return isBgmMuted; }
    }

    public bool IsSfxMuted
    {
        get { return isSfxMuted; }
    }

    public bool IsTurnAlertOn
    {
        get { return isTurnAlertOn; }
    }

    private void Awake()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;

        LoadSettings();
        ApplyVolumes();
    }

    private void Start()
    {
        if (playBgmOnStart)
        {
            PlayBgm();
        }
    }

    public void PlayBgm()
    {
        if (bgmClip != null)
        {
            if (bgmSource.clip != bgmClip)
            {
                bgmSource.clip = bgmClip;
            }
            if (bgmSource.isPlaying == false)
            {
                bgmSource.Play();
            }
        }
    }

    public void StopBgm()
    {
        bgmSource.Stop();
    }

    public void PlaySfx(Sfx sfx)
    {
        PlayClip(GetClip(sfx));
    }

    // 버튼 onClick에 바로 연결할 수 있도록 따로 둠
    public void PlayButton()
    {
        PlayClip(buttonClip);
    }

    public void PlayTurnAlert()
    {
        if (isTurnAlertOn)
        {
            PlayClip(turnAlertClip);
        }
    }

    // 효과음은 겹쳐서 재생 (같은 소리가 연달아 나도 앞 소리를 끊지 않음)
    public void PlayClip(AudioClip clip)
    {
        if (clip != null && isSfxMuted == false && sfxVolume > 0)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    public void SetBgmVolume(int volume)
    {
        bgmVolume = Mathf.Clamp(volume, 0, MaxVolume);
        SaveAndApply();
    }

    public void SetSfxVolume(int volume)
    {
        sfxVolume = Mathf.Clamp(volume, 0, MaxVolume);
        SaveAndApply();
    }

    public void SetBgmMuted(bool isMuted)
    {
        isBgmMuted = isMuted;
        SaveAndApply();
    }

    public void SetSfxMuted(bool isMuted)
    {
        isSfxMuted = isMuted;
        SaveAndApply();
    }

    public void SetTurnAlertOn(bool isOn)
    {
        isTurnAlertOn = isOn;
        SaveAndApply();
    }

    private AudioClip GetClip(Sfx sfx)
    {
        AudioClip clip = null;
        switch (sfx)
        {
            case Sfx.Button:
                clip = buttonClip;
                break;
            case Sfx.Put:
                clip = putClip;
                break;
            case Sfx.Pass:
                clip = passClip;
                break;
            case Sfx.Receive:
                clip = receiveClip;
                break;
            case Sfx.ChipGather:
                clip = chipGatherClip;
                break;
            case Sfx.TimerWarning:
                clip = timerWarningClip;
                break;
            case Sfx.Error:
                clip = errorClip;
                break;
            case Sfx.OtherTurn:
                clip = otherTurnClip;
                break;
            case Sfx.Select:
                clip = selectClip;
                break;
        }
        return clip;
    }

    private void LoadSettings()
    {
        bgmVolume = Mathf.Clamp(PlayerPrefs.GetInt(BgmVolumeKey, defaultBgmVolume), 0, MaxVolume);
        sfxVolume = Mathf.Clamp(PlayerPrefs.GetInt(SfxVolumeKey, defaultSfxVolume), 0, MaxVolume);
        isBgmMuted = PlayerPrefs.GetInt(BgmMutedKey, 0) == 1;
        isSfxMuted = PlayerPrefs.GetInt(SfxMutedKey, 0) == 1;
        isTurnAlertOn = PlayerPrefs.GetInt(TurnAlertKey, 1) == 1;
    }

    private void SaveAndApply()
    {
        PlayerPrefs.SetInt(BgmVolumeKey, bgmVolume);
        PlayerPrefs.SetInt(SfxVolumeKey, sfxVolume);
        PlayerPrefs.SetInt(BgmMutedKey, ToInt(isBgmMuted));
        PlayerPrefs.SetInt(SfxMutedKey, ToInt(isSfxMuted));
        PlayerPrefs.SetInt(TurnAlertKey, ToInt(isTurnAlertOn));
        PlayerPrefs.Save();

        ApplyVolumes();
        if (SettingsChanged != null)
        {
            SettingsChanged();
        }
    }

    // 음소거면 0, 아니면 0~100을 0~1로
    private void ApplyVolumes()
    {
        bgmSource.volume = GetSourceVolume(bgmVolume, isBgmMuted);
        sfxSource.volume = GetSourceVolume(sfxVolume, isSfxMuted);
    }

    private float GetSourceVolume(int volume, bool isMuted)
    {
        float result = 0f;
        if (isMuted == false)
        {
            result = (float)volume / MaxVolume;
        }
        return result;
    }

    private int ToInt(bool value)
    {
        int result = 0;
        if (value)
        {
            result = 1;
        }
        return result;
    }
}
