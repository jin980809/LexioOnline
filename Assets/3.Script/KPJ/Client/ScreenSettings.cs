using System;
using System.Collections.Generic;
using UnityEngine;

// 화면 설정: 16:9 해상도 목록 중 하나로 창 크기를 바꾸고, 전체 화면(테두리 없는 창, 모니터 해상도)을 켜고 끔
// 모니터보다 큰 해상도는 목록에서 뺌. 설정은 PlayerPrefs에 저장되어 시작할 때 다시 적용됨
// 에디터에서는 Screen.SetResolution이 동작하지 않으므로 실제 창 크기 변화는 빌드에서 확인해야 함
public class ScreenSettings : MonoBehaviour
{
    private const string WidthKey = "Lexio.Screen.Width";
    private const string HeightKey = "Lexio.Screen.Height";
    private const string FullScreenKey = "Lexio.Screen.FullScreen";

    // 작은 것부터 순서대로 (16:9만)
    [SerializeField] private Vector2Int[] resolutionCandidates =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
        new Vector2Int(3840, 2160)
    };

    [Header("Default (저장된 설정이 없을 때)")]
    [SerializeField] private Vector2Int defaultResolution = new Vector2Int(1920, 1080);
    [SerializeField] private bool defaultFullScreen = false;

    // 이 모니터에서 고를 수 있는 해상도 (모니터보다 큰 것은 뺌)
    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private int resolutionIndex;
    private bool isFullScreen;

    // 설정이 바뀌면 알림 (설정창 UI 갱신용)
    public event Action SettingsChanged;

    public int ResolutionCount
    {
        get { return resolutions.Count; }
    }

    // 창 모드에서 쓰는 해상도 위치 (전체 화면 중에도 기억해 두었다가 창 모드로 돌아갈 때 씀)
    public int ResolutionIndex
    {
        get { return resolutionIndex; }
    }

    public bool IsFullScreen
    {
        get { return isFullScreen; }
    }

    // 전체 화면에서 쓰는 해상도
    public Vector2Int MonitorResolution
    {
        get { return new Vector2Int(Display.main.systemWidth, Display.main.systemHeight); }
    }

    private void Awake()
    {
        BuildResolutionList();
        LoadSettings();
        ApplyScreen();
    }

    public Vector2Int GetResolution(int index)
    {
        Vector2Int result = Vector2Int.zero;
        if (index >= 0 && index < resolutions.Count)
        {
            result = resolutions[index];
        }
        return result;
    }

    public void SetResolutionIndex(int index)
    {
        int clamped = Mathf.Clamp(index, 0, resolutions.Count - 1);
        if (clamped != resolutionIndex)
        {
            resolutionIndex = clamped;
            SaveAndApply();
        }
    }

    public void SetFullScreen(bool isOn)
    {
        if (isOn != isFullScreen)
        {
            isFullScreen = isOn;
            SaveAndApply();
        }
    }

    private void BuildResolutionList()
    {
        Vector2Int monitor = MonitorResolution;
        resolutions.Clear();
        for (int i = 0; i < resolutionCandidates.Length; i++)
        {
            Vector2Int candidate = resolutionCandidates[i];
            if (candidate.x <= monitor.x && candidate.y <= monitor.y)
            {
                resolutions.Add(candidate);
            }
        }

        // 모니터가 목록의 모든 해상도보다 작으면 가장 작은 것 하나는 남김
        if (resolutions.Count == 0 && resolutionCandidates.Length > 0)
        {
            resolutions.Add(resolutionCandidates[0]);
        }
    }

    private void LoadSettings()
    {
        int width = PlayerPrefs.GetInt(WidthKey, defaultResolution.x);
        resolutionIndex = FindIndexForWidth(width);
        isFullScreen = PlayerPrefs.GetInt(FullScreenKey, ToInt(defaultFullScreen)) == 1;
    }

    // 저장된(또는 기본) 해상도가 목록에 없으면(모니터가 작아서 빠진 경우) 그보다 작은 것 중 가장 큰 것
    private int FindIndexForWidth(int width)
    {
        int result = 0;
        for (int i = 0; i < resolutions.Count; i++)
        {
            if (resolutions[i].x <= width)
            {
                result = i;
            }
        }
        return result;
    }

    private void SaveAndApply()
    {
        Vector2Int resolution = GetResolution(resolutionIndex);
        PlayerPrefs.SetInt(WidthKey, resolution.x);
        PlayerPrefs.SetInt(HeightKey, resolution.y);
        PlayerPrefs.SetInt(FullScreenKey, ToInt(isFullScreen));
        PlayerPrefs.Save();

        ApplyScreen();
        if (SettingsChanged != null)
        {
            SettingsChanged();
        }
    }

    // 전체 화면 = 테두리 없는 창으로 모니터 해상도 그대로 (독점 전체 화면은 Alt+Tab이 느려서 쓰지 않음)
    private void ApplyScreen()
    {
        if (isFullScreen)
        {
            Vector2Int monitor = MonitorResolution;
            Screen.SetResolution(monitor.x, monitor.y, FullScreenMode.FullScreenWindow);
        }
        else
        {
            Vector2Int resolution = GetResolution(resolutionIndex);
            Screen.SetResolution(resolution.x, resolution.y, FullScreenMode.Windowed);
        }
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
