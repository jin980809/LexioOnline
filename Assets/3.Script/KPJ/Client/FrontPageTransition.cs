using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

// 로그인 화면 ↔ 메인 화면 전환 연출
// NYM 매칭 UI(NYMMatchmakingUI)의 externalPageSwitch를 켜면 이 컴포넌트가 두 화면을 켜고 끔 (로그인 상태 판단은 NYM과 같음: 접속 + 인증 완료)
//  - 로그인 성공: 로그인 창은 아래로, 로고는 위로 빠져나가고, 이어서 프로필 카드·메뉴 버튼·공지 바·회전 테이블이 차례로 들어옴
//  - 로그아웃·연결 끊김: 메인 화면이 짧게 빠져나간 뒤 로그인 화면이 들어옴
//  - 게임에서 돌아와 다시 켜질 때(OnEnable)는 연출 없이 지금 상태로 바로 맞춤
// 연출 중에는 두 화면 모두 눌리지 않음
public class FrontPageTransition : MonoBehaviour
{
    [Header("화면")]
    [SerializeField] private GameObject loginPage;
    [SerializeField] private GameObject mainPage;

    [Header("로그인 화면 요소 (빠져나감)")]
    [SerializeField] private RectTransform loginPanel;
    [SerializeField] private RectTransform logo;

    [Header("로그인 화면 배경 타일 (양옆으로 치워짐)")]
    [Tooltip("배경 타일 묶음(TileMouseMotion이 붙은 Tiles). 바로 아래 자식(타일마다 하나)을 옮기고, 둥둥 모션은 그 안쪽에서 그대로 돎. 메인 화면에서는 꺼짐")]
    [SerializeField] private Transform backgroundTiles;
    [Tooltip("타일이 치워지는 시간(초)")]
    [SerializeField] private float tileSeconds = 0.5f;
    [Tooltip("타일이 옆으로 날아가는 거리 (월드 단위, 화면 밖까지)")]
    [SerializeField] private float tileScatter = 14f;

    [Header("메인 화면 요소 (들어옴)")]
    [SerializeField] private RectTransform profileCard;
    [Tooltip("이 아래 버튼들이 위에서부터 차례로 튀어나옴")]
    [SerializeField] private RectTransform menu;
    [SerializeField] private RectTransform noticeBar;
    [Tooltip("메인 화면과 함께 아래에서 떠오르며 커지는 3D 오브젝트 (회전 테이블). 메인 화면이 꺼지면 같이 꺼짐")]
    [SerializeField] private Transform[] mainProps = new Transform[0];

    [Header("로그인 → 메인 시간(초)")]
    [Tooltip("로그인 창·로고가 빠져나가는 시간")]
    [SerializeField] private float exitSeconds = 0.3f;
    [Tooltip("빠져나가기 시작한 뒤 메인 화면 요소가 들어오기 시작할 때까지")]
    [SerializeField] private float enterDelay = 0.25f;
    [Tooltip("요소 하나가 들어오는 시간")]
    [SerializeField] private float itemSeconds = 0.4f;
    [Tooltip("요소끼리 들어오는 간격")]
    [SerializeField] private float stagger = 0.06f;

    [Header("메인 → 로그인 시간(초)")]
    [Tooltip("메인 화면이 빠져나가는 시간과 로그인 화면이 들어오는 시간 (각각)")]
    [SerializeField] private float logoutSeconds = 0.2f;

    [Header("움직이는 거리 (UI는 캔버스 단위, 3D는 월드 단위)")]
    [SerializeField] private float panelDrop = 140f;
    [SerializeField] private float logoRise = 180f;
    [SerializeField] private float cardSlide = 640f;
    [SerializeField] private float noticeRise = 200f;
    [SerializeField] private float propRise = 1.5f;

    private class UiItem
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 position;
        public Vector3 scale;
    }

    private class PropItem
    {
        public Transform target;
        public Vector3 position;
        public Vector3 scale;
    }

    private readonly List<UiItem> mainItems = new List<UiItem>();
    private readonly List<Vector2> mainOffsets = new List<Vector2>();
    private readonly List<bool> mainPops = new List<bool>();
    private readonly List<PropItem> props = new List<PropItem>();
    private readonly List<PropItem> tiles = new List<PropItem>();
    // 타일마다 날아갈 방향 (화면 왼쪽 타일은 -1, 오른쪽은 +1)
    private readonly List<float> tileSides = new List<float>();
    private UiItem panelItem;
    private UiItem logoItem;
    private CanvasGroup loginGroup;
    private CanvasGroup mainGroup;
    private bool isLoggedInShown;
    private bool isReady;

    private void Awake()
    {
        loginGroup = GetGroup(loginPage.transform);
        mainGroup = GetGroup(mainPage.transform);
        panelItem = CreateItem(loginPanel);
        logoItem = CreateItem(logo);
        AddMainItem(profileCard, new Vector2(-cardSlide, 0f), false);
        if (menu != null)
        {
            for (int i = 0; i < menu.childCount; i++)
            {
                AddMainItem(menu.GetChild(i) as RectTransform, Vector2.zero, true);
            }
        }
        AddMainItem(noticeBar, new Vector2(0f, -noticeRise), false);
        for (int i = 0; i < mainProps.Length; i++)
        {
            if (mainProps[i] != null)
            {
                PropItem prop = new PropItem();
                prop.target = mainProps[i];
                prop.position = mainProps[i].localPosition;
                prop.scale = mainProps[i].localScale;
                props.Add(prop);
            }
        }
        if (backgroundTiles != null)
        {
            for (int i = 0; i < backgroundTiles.childCount; i++)
            {
                Transform tile = backgroundTiles.GetChild(i);
                PropItem item = new PropItem();
                item.target = tile;
                item.position = tile.localPosition;
                item.scale = tile.localScale;
                tiles.Add(item);
                // 묶음은 카메라를 보고 있어서 x가 화면 가로. 한가운데 타일은 번갈아 나눔
                float side = Mathf.Sign(tile.localPosition.x);
                if (Mathf.Abs(tile.localPosition.x) < 0.01f)
                {
                    side = 1f;
                    if (i % 2 == 1)
                    {
                        side = -1f;
                    }
                }
                tileSides.Add(side);
            }
        }
        isReady = true;
    }

    private void OnEnable()
    {
        if (isReady)
        {
            Snap(IsLoggedIn());
        }
    }

    private void Start()
    {
        Snap(IsLoggedIn());
    }

    private void Update()
    {
        bool isLoggedIn = IsLoggedIn();
        if (isLoggedIn != isLoggedInShown)
        {
            isLoggedInShown = isLoggedIn;
            StopAllCoroutines();
            ResetAll();
            if (isLoggedIn)
            {
                StartCoroutine(PlayLogin());
            }
            else
            {
                StartCoroutine(PlayLogout());
            }
        }
    }

    // NYM 매칭 UI와 같은 기준 (임시 접속인 회원가입·아이디 확인은 인증 전이라 로그인으로 보지 않음)
    private bool IsLoggedIn()
    {
        return NetworkClient.isConnected && NetworkClient.connection != null && NetworkClient.connection.isAuthenticated;
    }

    // ---------------- 연출 ----------------

    private IEnumerator PlayLogin()
    {
        SetInteractable(false);
        loginPage.SetActive(true);
        mainPage.SetActive(true);
        SetPropsActive(true);
        ApplyMain(0f);

        int count = mainItems.Count + props.Count;
        float total = Mathf.Max(Mathf.Max(exitSeconds, tileSeconds), enterDelay + Mathf.Max(0, count - 1) * stagger + itemSeconds);
        float time = 0f;
        while (time < total)
        {
            time += Time.unscaledDeltaTime;
            ApplyLoginExit(Mathf.Clamp01(time / exitSeconds));
            ApplyTiles(Mathf.Clamp01(time / tileSeconds));
            ApplyMain(time - enterDelay);
            yield return null;
        }
        Snap(true);
    }

    private IEnumerator PlayLogout()
    {
        SetInteractable(false);
        mainPage.SetActive(true);
        loginPage.SetActive(false);
        float time = 0f;
        while (time < logoutSeconds)
        {
            time += Time.unscaledDeltaTime;
            ApplyMainExit(Mathf.Clamp01(time / logoutSeconds));
            yield return null;
        }

        mainPage.SetActive(false);
        SetPropsActive(false);
        ResetAll();
        loginPage.SetActive(true);
        SetTilesActive(true);
        // 타일은 양옆에서 다시 날아 들어옴 (로그인 화면과 같은 시간)
        float enterSeconds = Mathf.Max(logoutSeconds, tileSeconds);
        time = 0f;
        while (time < enterSeconds)
        {
            time += Time.unscaledDeltaTime;
            ApplyLoginExit(1f - EaseOutCubic(Mathf.Clamp01(time / logoutSeconds)));
            ApplyTiles(1f - EaseOutCubic(Mathf.Clamp01(time / tileSeconds)));
            yield return null;
        }
        Snap(false);
    }

    // progress 0 = 제자리, 1 = 화면 밖 (양옆으로 날아가며 작아짐)
    private void ApplyTiles(float progress)
    {
        float eased = EaseInBack(progress);
        for (int i = 0; i < tiles.Count; i++)
        {
            tiles[i].target.localPosition = tiles[i].position + Vector3.right * (tileSides[i] * tileScatter * eased);
            tiles[i].target.localScale = tiles[i].scale * Mathf.Max(0f, 1f - 0.5f * progress);
        }
    }

    private void SetTilesActive(bool isActive)
    {
        if (backgroundTiles != null && backgroundTiles.gameObject.activeSelf != isActive)
        {
            backgroundTiles.gameObject.SetActive(isActive);
        }
    }

    // progress 0 = 제자리, 1 = 다 빠져나감 (살짝 부풀었다가 빠짐)
    private void ApplyLoginExit(float progress)
    {
        float eased = EaseInBack(progress);
        ApplyUi(panelItem, new Vector2(0f, -panelDrop * eased), Mathf.LerpUnclamped(1f, 0.85f, eased), 1f - progress);
        ApplyUi(logoItem, new Vector2(0f, logoRise * eased), Mathf.LerpUnclamped(1f, 1.1f, eased), 1f - progress);
    }

    // elapsed = 메인 요소가 들어오기 시작한 뒤 지난 시간. 요소마다 stagger만큼 늦게 시작
    private void ApplyMain(float elapsed)
    {
        for (int i = 0; i < mainItems.Count; i++)
        {
            float progress = Mathf.Clamp01((elapsed - i * stagger) / itemSeconds);
            float eased = EaseOutBack(progress);
            float scale = 1f;
            if (mainPops[i])
            {
                scale = eased;
            }
            ApplyUi(mainItems[i], mainOffsets[i] * (1f - eased), scale, Mathf.Clamp01(progress * 2f));
        }
        for (int i = 0; i < props.Count; i++)
        {
            float progress = Mathf.Clamp01((elapsed - (mainItems.Count + i) * stagger) / itemSeconds);
            float eased = EaseOutBack(progress);
            props[i].target.localPosition = props[i].position + Vector3.down * (propRise * (1f - eased));
            props[i].target.localScale = props[i].scale * Mathf.Max(0f, eased);
        }
    }

    // progress 0 = 제자리, 1 = 다 빠져나감
    private void ApplyMainExit(float progress)
    {
        float eased = EaseInBack(progress);
        for (int i = 0; i < mainItems.Count; i++)
        {
            float scale = 1f;
            if (mainPops[i])
            {
                scale = 1f - eased;
            }
            ApplyUi(mainItems[i], mainOffsets[i] * eased, scale, 1f - progress);
        }
        for (int i = 0; i < props.Count; i++)
        {
            props[i].target.localScale = props[i].scale * Mathf.Max(0f, 1f - eased);
        }
    }

    // 연출 없이 바로 맞춤 (연출이 끝났을 때, 다시 켜졌을 때)
    private void Snap(bool isLoggedIn)
    {
        StopAllCoroutines();
        isLoggedInShown = isLoggedIn;
        ResetAll();
        loginPage.SetActive(isLoggedIn == false);
        mainPage.SetActive(isLoggedIn);
        SetPropsActive(isLoggedIn);
        SetTilesActive(isLoggedIn == false);
        SetInteractable(true);
    }

    // ---------------- 공통 ----------------

    private void ResetAll()
    {
        ApplyUi(panelItem, Vector2.zero, 1f, 1f);
        ApplyUi(logoItem, Vector2.zero, 1f, 1f);
        for (int i = 0; i < mainItems.Count; i++)
        {
            ApplyUi(mainItems[i], Vector2.zero, 1f, 1f);
        }
        for (int i = 0; i < props.Count; i++)
        {
            props[i].target.localPosition = props[i].position;
            props[i].target.localScale = props[i].scale;
        }
        for (int i = 0; i < tiles.Count; i++)
        {
            tiles[i].target.localPosition = tiles[i].position;
            tiles[i].target.localScale = tiles[i].scale;
        }
    }

    private void ApplyUi(UiItem item, Vector2 offset, float scale, float alpha)
    {
        if (item != null)
        {
            item.rect.anchoredPosition = item.position + offset;
            item.rect.localScale = item.scale * scale;
            item.group.alpha = alpha;
        }
    }

    private void SetInteractable(bool isInteractable)
    {
        loginGroup.interactable = isInteractable;
        loginGroup.blocksRaycasts = isInteractable;
        mainGroup.interactable = isInteractable;
        mainGroup.blocksRaycasts = isInteractable;
    }

    private void SetPropsActive(bool isActive)
    {
        for (int i = 0; i < props.Count; i++)
        {
            if (props[i].target.gameObject.activeSelf != isActive)
            {
                props[i].target.gameObject.SetActive(isActive);
            }
        }
    }

    private void AddMainItem(RectTransform rect, Vector2 offset, bool isPop)
    {
        UiItem item = CreateItem(rect);
        if (item != null)
        {
            mainItems.Add(item);
            mainOffsets.Add(offset);
            mainPops.Add(isPop);
        }
    }

    private UiItem CreateItem(RectTransform rect)
    {
        UiItem item = null;
        if (rect != null)
        {
            item = new UiItem();
            item.rect = rect;
            item.group = GetGroup(rect);
            item.position = rect.anchoredPosition;
            item.scale = rect.localScale;
        }
        return item;
    }

    private CanvasGroup GetGroup(Transform target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.gameObject.AddComponent<CanvasGroup>();
        }
        return group;
    }

    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private float EaseInBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return c3 * x * x * x - c1 * x * x;
    }

    private float EaseOutCubic(float x)
    {
        return 1f - Mathf.Pow(1f - x, 3f);
    }
}
