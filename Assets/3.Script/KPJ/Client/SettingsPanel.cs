using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 설정창(사운드 + 화면) UI. 값은 SoundManager / ScreenSettings가 갖고 있고, 여기는 입력을 넘기고 현재 값을 그리기만 함
// 창이 켜질 때와 설정이 바뀔 때마다 UI를 다시 맞춤 (UI를 바꿀 때는 입력 이벤트가 다시 불리지 않게 WithoutNotify 사용)
// 음소거: 크기 값은 그대로 두고 회색으로 표시. 크기를 0으로 내리면 음소거, 0보다 크게 올리면 음소거 해제
public class SettingsPanel : MonoBehaviour
{
    // 배경음 / 효과음 한 줄
    [System.Serializable]
    public class VolumeRow
    {
        // 켜짐 = 음소거 (Toggle의 graphic이 음소거 아이콘)
        public Toggle muteToggle;
        public Slider slider;
        public InputField valueInput;
        // 음소거면 회색 이미지로 바꿈
        public Image fillImage;
        public Image handleImage;
    }

    // 손잡이가 좌우로 움직이는 켜기/끄기 스위치
    [System.Serializable]
    public class SwitchToggle
    {
        public Toggle toggle;
        public Image background;
        public RectTransform knob;
    }

    private const int MaxVolume = 100;

    [Header("References")]
    [SerializeField] private SoundManager soundManager;
    [SerializeField] private ScreenSettings screenSettings;

    [Header("Sound")]
    [SerializeField] private VolumeRow bgmRow;
    [SerializeField] private VolumeRow sfxRow;
    [SerializeField] private SwitchToggle turnAlertSwitch;
    [SerializeField] private Sprite fillSprite;
    [SerializeField] private Sprite fillMuteSprite;
    [SerializeField] private Sprite handleSprite;
    [SerializeField] private Sprite handleMuteSprite;
    // 크기가 0인 채로 음소거를 풀면 이 크기로 되돌림 (0이면 풀어도 소리가 안 나므로)
    [SerializeField, Range(1, MaxVolume)] private int unmuteVolume = 50;

    [Header("Switch")]
    [SerializeField] private Sprite switchOnSprite;
    [SerializeField] private Sprite switchOffSprite;
    // 손잡이가 가운데에서 좌우로 떨어진 거리 (켜짐 = 오른쪽) / 한쪽 끝에서 반대쪽까지 가는 시간
    [SerializeField] private float switchKnobOffset = 25f;
    [SerializeField] private float switchSlideSeconds = 0.12f;

    [Header("Screen")]
    [SerializeField] private SwitchToggle fullScreenSwitch;
    [SerializeField] private Button resolutionPrevButton;
    [SerializeField] private Button resolutionNextButton;
    [SerializeField] private Text resolutionText;
    // 목록 위치 점의 부모. 첫 자식 점을 복제해서 해상도 개수만큼 맞춤
    [SerializeField] private RectTransform pageDotRoot;
    [SerializeField] private Vector2 dotSize = new Vector2(6f, 6f);
    [SerializeField] private Vector2 currentDotSize = new Vector2(18f, 6f);
    [SerializeField] private Color dotColor = new Color(0.8549f, 0.8353f, 0.9216f, 1f);
    [SerializeField] private Color currentDotColor = new Color(0.3137f, 0.7608f, 0.9608f, 1f);

    [Header("Quit")]
    // 게임 종료 버튼 → 종료 확인창을 띄움 (설정창은 그 아래에 열린 채로 둠)
    [SerializeField] private Button quitGameButton;
    [SerializeField] private QuitConfirmPopup quitConfirmPopup;

    private readonly List<Image> dots = new List<Image>();

    private void Awake()
    {
        CollectDots();

        bgmRow.muteToggle.onValueChanged.AddListener(OnBgmMuteChanged);
        bgmRow.slider.onValueChanged.AddListener(OnBgmSliderChanged);
        bgmRow.valueInput.onEndEdit.AddListener(OnBgmInputEnded);
        sfxRow.muteToggle.onValueChanged.AddListener(OnSfxMuteChanged);
        sfxRow.slider.onValueChanged.AddListener(OnSfxSliderChanged);
        sfxRow.valueInput.onEndEdit.AddListener(OnSfxInputEnded);
        turnAlertSwitch.toggle.onValueChanged.AddListener(OnTurnAlertChanged);

        fullScreenSwitch.toggle.onValueChanged.AddListener(OnFullScreenChanged);
        resolutionPrevButton.onClick.AddListener(OnResolutionPrevClicked);
        resolutionNextButton.onClick.AddListener(OnResolutionNextClicked);

        quitGameButton.onClick.AddListener(OnQuitGameClicked);
    }

    private void OnEnable()
    {
        if (soundManager != null)
        {
            soundManager.SettingsChanged += Refresh;
        }
        if (screenSettings != null)
        {
            screenSettings.SettingsChanged += Refresh;
        }

        Refresh();
        // 창을 열 때는 손잡이를 미끄러뜨리지 않고 바로 제자리에 둠
        SnapKnob(turnAlertSwitch);
        SnapKnob(fullScreenSwitch);
    }

    private void OnDisable()
    {
        if (soundManager != null)
        {
            soundManager.SettingsChanged -= Refresh;
        }
        if (screenSettings != null)
        {
            screenSettings.SettingsChanged -= Refresh;
        }
    }

    private void Update()
    {
        MoveKnob(turnAlertSwitch);
        MoveKnob(fullScreenSwitch);
    }

    // ---------------- 입력 ----------------

    private void OnBgmMuteChanged(bool isMuted)
    {
        ChangeMuted(true, isMuted);
    }

    private void OnSfxMuteChanged(bool isMuted)
    {
        ChangeMuted(false, isMuted);
    }

    private void OnBgmSliderChanged(float value)
    {
        ChangeVolume(true, Mathf.RoundToInt(value));
    }

    private void OnSfxSliderChanged(float value)
    {
        ChangeVolume(false, Mathf.RoundToInt(value));
    }

    private void OnBgmInputEnded(string text)
    {
        ChangeVolumeFromText(true, text);
    }

    private void OnSfxInputEnded(string text)
    {
        ChangeVolumeFromText(false, text);
    }

    private void OnTurnAlertChanged(bool isOn)
    {
        if (soundManager != null)
        {
            soundManager.SetTurnAlertOn(isOn);
        }
    }

    private void OnFullScreenChanged(bool isOn)
    {
        if (screenSettings != null)
        {
            screenSettings.SetFullScreen(isOn);
        }
    }

    private void OnResolutionPrevClicked()
    {
        if (screenSettings != null)
        {
            screenSettings.SetResolutionIndex(screenSettings.ResolutionIndex - 1);
        }
    }

    private void OnResolutionNextClicked()
    {
        if (screenSettings != null)
        {
            screenSettings.SetResolutionIndex(screenSettings.ResolutionIndex + 1);
        }
    }

    private void OnQuitGameClicked()
    {
        if (quitConfirmPopup != null)
        {
            quitConfirmPopup.Open();
        }
    }

    // 크기를 바꾸면 음소거도 크기에 맞춤 (0 = 음소거)
    private void ChangeVolume(bool isBgm, int volume)
    {
        if (soundManager != null)
        {
            int clamped = Mathf.Clamp(volume, 0, MaxVolume);
            if (isBgm)
            {
                soundManager.SetBgmVolume(clamped);
                soundManager.SetBgmMuted(clamped == 0);
            }
            else
            {
                soundManager.SetSfxVolume(clamped);
                soundManager.SetSfxMuted(clamped == 0);
            }
        }
    }

    // 숫자가 아니면 바꾸지 않고 원래 값으로 되돌려 표시
    private void ChangeVolumeFromText(bool isBgm, string text)
    {
        int volume;
        if (int.TryParse(text, out volume))
        {
            ChangeVolume(isBgm, volume);
        }
        Refresh();
    }

    private void ChangeMuted(bool isBgm, bool isMuted)
    {
        if (soundManager != null)
        {
            if (isBgm)
            {
                if (isMuted == false && soundManager.BgmVolume == 0)
                {
                    soundManager.SetBgmVolume(unmuteVolume);
                }
                soundManager.SetBgmMuted(isMuted);
            }
            else
            {
                if (isMuted == false && soundManager.SfxVolume == 0)
                {
                    soundManager.SetSfxVolume(unmuteVolume);
                }
                soundManager.SetSfxMuted(isMuted);
            }
        }
    }

    // ---------------- 표시 ----------------

    private void Refresh()
    {
        if (soundManager != null)
        {
            RefreshVolumeRow(bgmRow, soundManager.BgmVolume, soundManager.IsBgmMuted);
            RefreshVolumeRow(sfxRow, soundManager.SfxVolume, soundManager.IsSfxMuted);
            RefreshSwitch(turnAlertSwitch, soundManager.IsTurnAlertOn);
        }
        if (screenSettings != null)
        {
            RefreshScreen();
        }
    }

    private void RefreshVolumeRow(VolumeRow row, int volume, bool isMuted)
    {
        row.muteToggle.SetIsOnWithoutNotify(isMuted);
        row.slider.SetValueWithoutNotify(volume);
        row.valueInput.SetTextWithoutNotify(volume.ToString());

        Sprite fill = fillSprite;
        Sprite handle = handleSprite;
        if (isMuted)
        {
            fill = fillMuteSprite;
            handle = handleMuteSprite;
        }
        SetSprite(row.fillImage, fill);
        SetSprite(row.handleImage, handle);
    }

    private void RefreshSwitch(SwitchToggle target, bool isOn)
    {
        target.toggle.SetIsOnWithoutNotify(isOn);
        Sprite background = switchOffSprite;
        if (isOn)
        {
            background = switchOnSprite;
        }
        SetSprite(target.background, background);
    }

    // 전체 화면이면 모니터 해상도를 보여 주고 좌우 버튼은 막음 (창 모드로 돌아가면 고른 해상도로 돌아감)
    private void RefreshScreen()
    {
        bool isFullScreen = screenSettings.IsFullScreen;
        int index = screenSettings.ResolutionIndex;
        int count = screenSettings.ResolutionCount;
        RefreshSwitch(fullScreenSwitch, isFullScreen);

        Vector2Int shown = screenSettings.GetResolution(index);
        int currentDot = index;
        if (isFullScreen)
        {
            shown = screenSettings.MonitorResolution;
            currentDot = -1;
        }
        resolutionText.text = shown.x + " × " + shown.y;

        resolutionPrevButton.interactable = isFullScreen == false && index > 0;
        resolutionNextButton.interactable = isFullScreen == false && index < count - 1;
        RefreshDots(count, currentDot);
    }

    private void CollectDots()
    {
        dots.Clear();
        if (pageDotRoot != null)
        {
            for (int i = 0; i < pageDotRoot.childCount; i++)
            {
                Image dot = pageDotRoot.GetChild(i).GetComponent<Image>();
                if (dot != null)
                {
                    dots.Add(dot);
                }
            }
        }
    }

    // 점 개수를 해상도 개수에 맞추고(모자라면 첫 점 복제, 남으면 끔), 현재 위치만 길고 파랗게
    private void RefreshDots(int count, int currentIndex)
    {
        if (dots.Count > 0)
        {
            while (dots.Count < count)
            {
                dots.Add(Instantiate(dots[0], pageDotRoot));
            }

            for (int i = 0; i < dots.Count; i++)
            {
                Image dot = dots[i];
                bool isUsed = i < count;
                if (dot.gameObject.activeSelf != isUsed)
                {
                    dot.gameObject.SetActive(isUsed);
                }
                if (isUsed)
                {
                    bool isCurrent = i == currentIndex;
                    Vector2 size = dotSize;
                    Color color = dotColor;
                    if (isCurrent)
                    {
                        size = currentDotSize;
                        color = currentDotColor;
                    }
                    dot.rectTransform.sizeDelta = size;
                    dot.color = color;
                }
            }
        }
    }

    // 스위치 손잡이: 켜짐 = 오른쪽, 꺼짐 = 왼쪽. 매 프레임 목표 위치로 조금씩 이동
    private void MoveKnob(SwitchToggle target)
    {
        if (target.knob != null)
        {
            Vector2 position = target.knob.anchoredPosition;
            float goal = GetKnobX(target.toggle.isOn);
            if (position.x != goal)
            {
                float speed = switchKnobOffset * 2f / Mathf.Max(0.01f, switchSlideSeconds);
                position.x = Mathf.MoveTowards(position.x, goal, speed * Time.unscaledDeltaTime);
                target.knob.anchoredPosition = position;
            }
        }
    }

    private void SnapKnob(SwitchToggle target)
    {
        if (target.knob != null)
        {
            Vector2 position = target.knob.anchoredPosition;
            position.x = GetKnobX(target.toggle.isOn);
            target.knob.anchoredPosition = position;
        }
    }

    private float GetKnobX(bool isOn)
    {
        float result = -switchKnobOffset;
        if (isOn)
        {
            result = switchKnobOffset;
        }
        return result;
    }

    private void SetSprite(Image target, Sprite sprite)
    {
        if (target != null && sprite != null)
        {
            target.sprite = sprite;
        }
    }
}
