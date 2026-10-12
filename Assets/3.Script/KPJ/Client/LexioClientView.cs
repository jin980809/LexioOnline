using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public enum HandSortMode
{
    SuitFirst = 0,
    NumberFirst = 1
}

// 3D 테이블 표시: 내 손패(선택), 가운데 마지막 패, 상대 뒷면 타일
// 테이블은 XZ 평면, 내 자리는 -Z 쪽(화면 아래)
// 타일 오브젝트는 풀링해서 재사용 (타일 ID가 고유하므로 ID별로 1개씩 보관)
public class LexioClientView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LexioNetworkManager networkManager;
    [SerializeField] private TileDatabase tileDatabase;
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Camera viewCamera;

    [Header("Tile")]
    [SerializeField] private float tileScale = 0.3f;
    [SerializeField] private float tileGap = 0.06f;
    [SerializeField] private float selectedLift = 0.35f;
    [SerializeField] private Color selectedTint = new Color(1f, 0.95f, 0.7f, 1f);

    [Header("Hand (내 패)")]
    [SerializeField] private Vector3 handCenter = new Vector3(0f, 0.55f, -4.5f);
    [SerializeField] private Vector3 handTileEuler = new Vector3(20f, 0f, 0f);

    [Header("Hand Slots (손패 칸 / 드래그 배치)")]
    // 최대 손패 장수보다 작으면 Awake에서 늘림
    [SerializeField] private int handSlotColumns = 15;
    // 0번 줄이 앞줄(내 쪽), 다음 줄은 그 뒤에 세워짐(계단식 패 받침대처럼). 처음 받은 패는 앞줄부터 채움
    [SerializeField] private int handSlotRows = 2;
    // 앞뒤 줄 사이 간격 (앞줄 타일이 기울어진 만큼은 자동으로 더 띄움)
    [SerializeField] private float handRowGap = 0.08f;
    // 뒷줄을 앞줄 너머로 보이게 올리는 높이 (타일 세로 길이 대비 비율)
    [SerializeField] private float handRowRise = 0.4f;
    [SerializeField] private float dragThresholdPixels = 10f;
    [SerializeField] private float dragLift = 0.5f;
    [SerializeField] private Material slotMaterial;
    [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color slotHighlightColor = new Color(1f, 0.85f, 0.2f, 0.45f);

    [Header("Center (마지막 낸 패)")]
    [SerializeField] private Vector3 centerPosition = new Vector3(0f, 0.25f, 0f);
    [SerializeField] private Vector3 centerTileEuler = new Vector3(90f, 0f, 0f);

    [Header("Opponents (상대)")]
    [SerializeField] private float opponentRadiusX = 5.5f;
    [SerializeField] private float opponentRadiusZ = 3.2f;
    [SerializeField] private float opponentOffsetZ = 0.8f;
    // 5인(상대 4명)일 때 자리끼리 줄 끝이 붙지 않도록 배치 타원을 키우는 배율
    [SerializeField] private float fivePlayerRadiusScale = 1.5f;
    [SerializeField] private float opponentTileHeight = 0.55f;
    [SerializeField] private float opponentSpacingRatio = 0.55f;
    [SerializeField] private Vector3 backTileEuler = Vector3.zero;
    [SerializeField] private float labelHeight = 1.4f;

    [Header("Deal (라운드 시작 패 돌리기)")]
    // 서버 LexioTable.dealSeconds보다 짧아야 연출 중에 턴이 시작되지 않음
    [SerializeField] private float dealSeconds = 1.8f;
    [SerializeField] private float dealFlightSeconds = 0.35f;
    [SerializeField] private float dealArcHeight = 1.2f;
    [SerializeField] private Vector3 dealOrigin = new Vector3(0f, 0.6f, 0f);

    // 중앙에서 자리로 날아가는 타일 1장
    private struct DealFlight
    {
        public Transform target;
        public TileView handView;
        public Vector3 endPosition;
        public Quaternion endRotation;
        public float startTime;
        public bool isLanded;
    }

    [Header("Play (낸 패가 가운데로 날아가기)")]
    [SerializeField] private float playFlightSeconds = 0.3f;
    [SerializeField] private float playStagger = 0.04f;
    [SerializeField] private float playArcHeight = 0.6f;
    // 손패에서 빠진 타일 위치를 이 시간 동안만 출발점으로 사용 (가운데 패 동기화가 손패보다 늦게 오는 경우 대비)
    [SerializeField] private float playOriginKeepSeconds = 1f;

    // 낸 자리에서 가운데로 날아가는 타일 1장
    private struct PlayFlight
    {
        public TileView view;
        public Vector3 startPosition;
        public Quaternion startRotation;
        public Vector3 endPosition;
        public Quaternion endRotation;
        public float startTime;
        public bool isFlip;
        public bool isLanded;
    }

    // 손패에서 빠질 때의 타일 위치
    private struct PlayOrigin
    {
        public Vector3 position;
        public Quaternion rotation;
        public float time;
    }

    private readonly List<TileView> handViews = new List<TileView>();
    private readonly List<TileView> centerViews = new List<TileView>();
    private readonly Dictionary<byte, TileView> handPool = new Dictionary<byte, TileView>();
    private readonly Dictionary<byte, TileView> centerPool = new Dictionary<byte, TileView>();
    private readonly List<GameObject> backPool = new List<GameObject>();
    private readonly Dictionary<Mesh, Material> outlineCache = new Dictionary<Mesh, Material>();
    private readonly List<Tile> receivedTiles = new List<Tile>();
    private readonly List<byte> shownCenterIds = new List<byte>();
    private readonly int[] shownTileCounts = new int[GameRules.MaxPlayers];
    private readonly Vector3[] seatAnchors = new Vector3[GameRules.MaxPlayers];
    private readonly bool[] hasSeatAnchor = new bool[GameRules.MaxPlayers];
    private readonly int[] seatBackStarts = new int[GameRules.MaxPlayers];
    private readonly List<DealFlight> dealFlights = new List<DealFlight>();
    private readonly List<PlayFlight> playFlights = new List<PlayFlight>();
    private readonly Dictionary<byte, PlayOrigin> playOrigins = new Dictionary<byte, PlayOrigin>();
    private bool isDealing;
    private float dealStartTime;
    private float playStartTime;
    private Comparison<TileView> suitFirstComparison;
    private Comparison<TileView> numberFirstComparison;
    private int sortMaxNumber;
    private int activeBackCount;
    private int shownMySeat = -1;
    private int shownPlayerCount;
    private int cachedVersion = -1;
    private bool isTileMeasured;
    private Vector3 tileSize;
    private Vector3 tileCenterOffset;

    // 칸 번호별 손패 (null = 빈칸). handViews는 손에 든 타일 목록이고 위치는 이 배열이 결정
    private TileView[] handSlots;
    private Renderer[] slotMarkers;
    private MaterialPropertyBlock slotPropertyBlock;
    private int slotColorId;
    private TileView pressedView;
    private Vector2 pressScreenPosition;
    private TileView draggingView;
    private int dragOriginSlot = -1;
    private int dragTargetSlot = -1;

    public event Action SelectionChanged;

    // 패 돌리는 중에는 선택/정렬/제출 불가
    public bool IsDealing
    {
        get { return isDealing; }
    }

    public Camera ViewCamera
    {
        get { return viewCamera; }
    }

    private void Awake()
    {
        suitFirstComparison = CompareSuitFirst;
        numberFirstComparison = CompareNumberFirst;

        int maxTiles = 0;
        for (int playerCount = GameRules.MinPlayers; playerCount <= GameRules.MaxPlayers; playerCount++)
        {
            maxTiles = Mathf.Max(maxTiles, GameRules.GetTilesPerPlayer(playerCount));
        }
        handSlotRows = Mathf.Max(1, handSlotRows);
        if (handSlotColumns * handSlotRows < maxTiles)
        {
            handSlotColumns = Mathf.CeilToInt(maxTiles / (float)handSlotRows);
        }
        handSlots = new TileView[handSlotColumns * handSlotRows];
        slotPropertyBlock = new MaterialPropertyBlock();
        slotColorId = Shader.PropertyToID("_BaseColor");

        if (slotMaterial == null)
        {
            Debug.LogWarning("[Lexio] LexioClientView에 slotMaterial이 없어 드래그 중 칸이 표시되지 않습니다. Lexio/Setup Project를 다시 실행하세요.");
        }

        // 참조가 비어 있으면 타일 대신 큐브가 표시됨 (예외로 서버 연결이 끊기지 않도록 미리 알림)
        if (tileDatabase == null)
        {
            Debug.LogError("[Lexio] LexioClientView에 TileDatabase가 연결되지 않았습니다. Assets/11.Data/TileDatabase.asset을 연결하세요.");
        }
    }

    private void OnEnable()
    {
        networkManager.HandReceived += OnHandReceived;
        networkManager.ClientDisconnected += ClearAll;
    }

    private void OnDisable()
    {
        networkManager.HandReceived -= OnHandReceived;
        networkManager.ClientDisconnected -= ClearAll;
    }

    private void OnDestroy()
    {
        foreach (Material material in outlineCache.Values)
        {
            Destroy(material);
        }
        outlineCache.Clear();
    }

    private void Update()
    {
        if (isDealing)
        {
            UpdateDeal();
        }
        else
        {
            HandleHandInput();
        }
        UpdatePlay();

        LexioTable table = networkManager.Table;
        if (table != null && table.Version != cachedVersion)
        {
            cachedVersion = table.Version;
            RefreshCenter(table);
            // 패 돌리는 중에는 상대 타일을 연출이 직접 보여줌 (끝나면 cachedVersion을 비워서 다시 갱신)
            if (isDealing == false)
            {
                RefreshOpponents(table);
            }
        }
    }

    // 선택된 타일을 results에 채움 (매번 새 리스트를 만들지 않도록 호출 측 리스트 재사용)
    public void GetSelectedTiles(List<Tile> results)
    {
        results.Clear();
        for (int i = 0; i < handViews.Count; i++)
        {
            if (handViews[i].IsSelected)
            {
                results.Add(handViews[i].Tile);
            }
        }
    }

    public void ClearSelection()
    {
        for (int i = 0; i < handViews.Count; i++)
        {
            handViews[i].SetSelected(false);
        }
        NotifySelectionChanged();
    }

    public void SortHand(HandSortMode mode)
    {
        if (isDealing == false)
        {
            sortMaxNumber = GetMaxNumber();
            if (mode == HandSortMode.SuitFirst)
            {
                handViews.Sort(suitFirstComparison);
            }
            else
            {
                handViews.Sort(numberFirstComparison);
            }
            FillSlotsFromLeft();
            LayoutHand();
        }
    }

    // 상대 자리 라벨을 띄울 월드 좌표
    public bool TryGetSeatAnchor(int seat, out Vector3 position)
    {
        bool result = false;
        position = Vector3.zero;
        if (seat >= 0 && seat < hasSeatAnchor.Length && hasSeatAnchor[seat])
        {
            position = seatAnchors[seat];
            result = true;
        }
        return result;
    }

    // ---------------- 손패 ----------------

    private void OnHandReceived(byte[] tileIds, bool isNewDeal, int mySeat, int playerCount)
    {
        receivedTiles.Clear();
        for (int i = 0; i < tileIds.Length; i++)
        {
            receivedTiles.Add(new Tile(tileIds[i]));
        }

        // 끌고 있던 타일이 빠질 수 있으므로(시간 초과 자동 제출 등) 드래그는 취소
        CancelDrag();

        if (isNewDeal)
        {
            // 새 라운드: 받은 순서대로 놓기
            ResetDeal();
            ReleaseHand();
            playOrigins.Clear();
            for (int i = 0; i < receivedTiles.Count; i++)
            {
                handViews.Add(AcquireTile(handPool, receivedTiles[i], true));
            }
            FillSlotsFromLeft();
        }
        else
        {
            // 낸 패만 빼고 현재 배치 순서는 유지
            for (int i = handViews.Count - 1; i >= 0; i--)
            {
                if (receivedTiles.Contains(handViews[i].Tile) == false)
                {
                    RememberPlayOrigin(handViews[i]);
                    RemoveFromSlot(handViews[i]);
                    ReleaseTile(handViews[i]);
                    handViews.RemoveAt(i);
                }
            }

            for (int i = 0; i < receivedTiles.Count; i++)
            {
                if (IsInHand(receivedTiles[i]) == false)
                {
                    TileView view = AcquireTile(handPool, receivedTiles[i], true);
                    handViews.Add(view);
                    PutInFirstEmptySlot(view);
                }
            }
        }

        LayoutHand();
        if (isNewDeal)
        {
            StartDeal(mySeat, playerCount);
        }
        NotifySelectionChanged();
    }

    // 짧게 클릭하면 선택/해제, 누른 채 일정 거리 이상 끌면 칸 이동
    private void HandleHandInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && viewCamera != null)
        {
            Vector2 screenPosition = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressedView = FindHandTileAt(screenPosition);
                pressScreenPosition = screenPosition;
            }

            if (pressedView != null && mouse.leftButton.isPressed)
            {
                if (draggingView == null && (screenPosition - pressScreenPosition).magnitude >= dragThresholdPixels)
                {
                    BeginDrag(pressedView);
                }

                if (draggingView != null)
                {
                    UpdateDrag(screenPosition);
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (draggingView != null)
                {
                    EndDrag();
                }
                else if (pressedView != null)
                {
                    pressedView.SetSelected(pressedView.IsSelected == false);
                    NotifySelectionChanged();
                }
                pressedView = null;
            }
        }
    }

    private TileView FindHandTileAt(Vector2 screenPosition)
    {
        TileView result = null;
        bool isOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (isOverUi == false)
        {
            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                TileView view = hit.collider.GetComponentInParent<TileView>();
                if (view != null && handViews.Contains(view))
                {
                    result = view;
                }
            }
        }
        return result;
    }

    private void LayoutHand()
    {
        Quaternion rotation = Quaternion.Euler(handTileEuler);
        for (int slot = 0; slot < handSlots.Length; slot++)
        {
            TileView view = handSlots[slot];
            if (view != null)
            {
                view.transform.rotation = rotation;
                view.SetBasePosition(GetSlotPosition(slot));
            }
        }
    }

    // ---------------- 손패 칸 / 드래그 ----------------

    // 칸 중심(타일 피벗 기준) 위치. 각 줄은 handCenter를 중심으로 가로로 놓임
    // 칸 번호 = 줄 * 열 개수 + 열. 앞줄(0번)이 handCenter 위치이고 뒷줄은 GetRowOffset만큼 뒤로 물러나며 올라감
    private Vector3 GetSlotPosition(int slot)
    {
        int column = slot % handSlotColumns;
        int row = slot / handSlotColumns;
        float spacing = GetSlotSpacing();
        float startX = -(handSlotColumns - 1) * spacing * 0.5f;
        return handCenter + new Vector3(startX + column * spacing, 0f, 0f) + GetRowOffset() * row;
    }

    private float GetSlotSpacing()
    {
        return GetTileWidth() + tileGap;
    }

    // 한 줄 뒤로 갈 때 이동량. 뒤로(+Z) = 타일 두께 + 앞줄 타일이 뒤로 기운 만큼 + 간격, 위로(+Y) = 세로 길이 * handRowRise
    private Vector3 GetRowOffset()
    {
        MeasureTile();
        float lean = Mathf.Abs(Mathf.Sin(handTileEuler.x * Mathf.Deg2Rad)) * tileSize.y;
        float depth = tileSize.z + lean + handRowGap;
        float rise = tileSize.y * handRowRise;
        return new Vector3(0f, rise, depth);
    }

    // 정렬/새 라운드: handViews 순서대로 1번 칸부터 채움
    private void FillSlotsFromLeft()
    {
        Array.Clear(handSlots, 0, handSlots.Length);
        for (int i = 0; i < handViews.Count && i < handSlots.Length; i++)
        {
            handSlots[i] = handViews[i];
        }
    }

    private void PutInFirstEmptySlot(TileView view)
    {
        bool isPlaced = false;
        for (int slot = 0; slot < handSlots.Length && isPlaced == false; slot++)
        {
            if (handSlots[slot] == null)
            {
                handSlots[slot] = view;
                isPlaced = true;
            }
        }
    }

    private void RemoveFromSlot(TileView view)
    {
        int slot = FindSlot(view);
        if (slot >= 0)
        {
            handSlots[slot] = null;
        }
    }

    private int FindSlot(TileView view)
    {
        int result = -1;
        for (int slot = 0; slot < handSlots.Length; slot++)
        {
            if (handSlots[slot] == view)
            {
                result = slot;
            }
        }
        return result;
    }

    private void BeginDrag(TileView view)
    {
        draggingView = view;
        dragOriginSlot = FindSlot(view);
        dragTargetSlot = dragOriginSlot;
        ShowSlotMarkers(true);
        RefreshSlotHighlight();
    }

    // 마우스와 화면상 가장 가까운 칸을 놓을 칸으로 정하고, 끄는 타일은 그 줄 위에서 마우스를 가로로 따라감
    private void UpdateDrag(Vector2 screenPosition)
    {
        int slot = FindNearestSlotOnScreen(screenPosition);
        if (slot != dragTargetSlot)
        {
            dragTargetSlot = slot;
            RefreshSlotHighlight();
        }

        // 놓을 줄의 타일 면에 마우스를 투영해서 가로 위치를 구함
        Quaternion rotation = Quaternion.Euler(handTileEuler);
        Vector3 rowOrigin = GetSlotPosition(slot - slot % handSlotColumns);
        Ray ray = viewCamera.ScreenPointToRay(screenPosition);
        Plane plane = new Plane(rotation * Vector3.forward, rowOrigin);
        if (plane.Raycast(ray, out float enter))
        {
            // 마우스는 타일 중심을 잡고 있으므로 피벗 기준으로 되돌림
            Vector3 point = ray.GetPoint(enter) - rotation * tileCenterOffset;
            float x = Mathf.Clamp(point.x - rowOrigin.x, 0f, (handSlotColumns - 1) * GetSlotSpacing());

            // 다른 타일에 가려지지 않도록 카메라 쪽으로 띄움
            Vector3 position = rowOrigin + new Vector3(x, 0f, 0f);
            Vector3 toCamera = (viewCamera.transform.position - position).normalized;
            draggingView.transform.position = position + toCamera * dragLift;
        }
    }

    // 줄이 앞뒤로 겹쳐 보이므로 3D 면 대신 화면 좌표로 칸 중심과의 거리를 비교
    private int FindNearestSlotOnScreen(Vector2 screenPosition)
    {
        Vector3 centerOffset = Quaternion.Euler(handTileEuler) * tileCenterOffset;
        int result = 0;
        float bestDistance = float.MaxValue;
        for (int slot = 0; slot < handSlots.Length; slot++)
        {
            Vector3 slotScreen = viewCamera.WorldToScreenPoint(GetSlotPosition(slot) + centerOffset);
            float distance = (new Vector2(slotScreen.x, slotScreen.y) - screenPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                result = slot;
            }
        }
        return result;
    }

    // 놓은 칸에 다른 패가 있으면 서로 자리를 바꾸고, 빈칸이면 원래 칸이 빈칸이 됨
    private void EndDrag()
    {
        if (dragOriginSlot >= 0 && dragTargetSlot >= 0 && dragTargetSlot != dragOriginSlot)
        {
            TileView other = handSlots[dragTargetSlot];
            handSlots[dragTargetSlot] = draggingView;
            handSlots[dragOriginSlot] = other;
        }
        draggingView = null;
        dragOriginSlot = -1;
        dragTargetSlot = -1;
        ShowSlotMarkers(false);
        LayoutHand();
    }

    // 드래그 중 손패가 바뀌거나 접속이 끊긴 경우. 위치는 호출 측의 LayoutHand가 되돌림
    private void CancelDrag()
    {
        draggingView = null;
        pressedView = null;
        dragOriginSlot = -1;
        dragTargetSlot = -1;
        ShowSlotMarkers(false);
    }

    private void ShowSlotMarkers(bool isVisible)
    {
        if (isVisible)
        {
            CreateSlotMarkers();
        }

        if (slotMarkers != null)
        {
            Quaternion rotation = Quaternion.Euler(handTileEuler);
            Vector3 centerOffset = rotation * tileCenterOffset;
            Vector3 markerScale = new Vector3(tileSize.x * 1.12f, tileSize.y * 1.06f, 1f);
            for (int slot = 0; slot < slotMarkers.Length; slot++)
            {
                GameObject marker = slotMarkers[slot].gameObject;
                if (isVisible)
                {
                    // 타일 중심에 놓고 타일보다 조금 크게 해서 테두리처럼 보이게 함 (빈칸은 판 전체가 보임)
                    marker.transform.SetPositionAndRotation(GetSlotPosition(slot) + centerOffset, rotation);
                    marker.transform.localScale = markerScale;
                }
                if (marker.activeSelf != isVisible)
                {
                    marker.SetActive(isVisible);
                }
            }
        }
    }

    // 처음 드래그할 때 한 번만 생성
    private void CreateSlotMarkers()
    {
        if (slotMarkers == null && slotMaterial != null)
        {
            MeasureTile();
            slotMarkers = new Renderer[handSlots.Length];
            for (int slot = 0; slot < slotMarkers.Length; slot++)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                marker.name = "HandSlot" + slot;
                marker.transform.SetParent(transform, false);
                Destroy(marker.GetComponent<Collider>());
                Renderer markerRenderer = marker.GetComponent<Renderer>();
                markerRenderer.sharedMaterial = slotMaterial;
                markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                markerRenderer.receiveShadows = false;
                marker.SetActive(false);
                slotMarkers[slot] = markerRenderer;
            }
        }
    }

    private void RefreshSlotHighlight()
    {
        if (slotMarkers != null)
        {
            for (int slot = 0; slot < slotMarkers.Length; slot++)
            {
                if (slot == dragTargetSlot)
                {
                    slotPropertyBlock.SetColor(slotColorId, slotHighlightColor);
                }
                else
                {
                    slotPropertyBlock.SetColor(slotColorId, slotColor);
                }
                slotMarkers[slot].SetPropertyBlock(slotPropertyBlock);
            }
        }
    }

    private bool IsInHand(Tile tile)
    {
        bool result = false;
        for (int i = 0; i < handViews.Count; i++)
        {
            if (handViews[i].Tile.Equals(tile))
            {
                result = true;
            }
        }
        return result;
    }

    private void ReleaseHand()
    {
        for (int i = 0; i < handViews.Count; i++)
        {
            ReleaseTile(handViews[i]);
        }
        handViews.Clear();
        Array.Clear(handSlots, 0, handSlots.Length);
    }

    private int CompareSuitFirst(TileView a, TileView b)
    {
        int result = a.Tile.Suit.CompareTo(b.Tile.Suit);
        if (result == 0)
        {
            result = a.Tile.GetRank(sortMaxNumber).CompareTo(b.Tile.GetRank(sortMaxNumber));
        }
        return result;
    }

    private int CompareNumberFirst(TileView a, TileView b)
    {
        return a.Tile.GetPower(sortMaxNumber).CompareTo(b.Tile.GetPower(sortMaxNumber));
    }

    // ---------------- 가운데 마지막 패 ----------------

    private void RefreshCenter(LexioTable table)
    {
        IReadOnlyList<byte> tileIds = table.LastPlayTiles;
        if (table.Phase == GamePhase.Waiting)
        {
            tileIds = null;
        }

        if (IsSameCenter(tileIds) == false)
        {
            ReleaseCenter();
            if (tileIds != null && tileIds.Count > 0)
            {
                // 게임 중에 새로 낸 패만 날아가는 연출 (패 돌리는 중이나 대기 상태는 바로 놓음)
                bool canAnimate = table.Phase == GamePhase.Playing && isDealing == false;
                float spacing = GetTileWidth() + tileGap;
                float startX = -(tileIds.Count - 1) * spacing * 0.5f;
                Quaternion rotation = Quaternion.Euler(centerTileEuler);
                for (int i = 0; i < tileIds.Count; i++)
                {
                    TileView view = AcquireTile(centerPool, new Tile(tileIds[i]), false);
                    view.transform.rotation = rotation;
                    view.SetBasePosition(centerPosition + new Vector3(startX + i * spacing, 0f, 0f));
                    centerViews.Add(view);
                    shownCenterIds.Add(tileIds[i]);
                    if (canAnimate)
                    {
                        StartPlayFlight(view, table.LastPlaySeat, i, tileIds.Count);
                    }
                }
                playStartTime = Time.time;
            }
        }
    }

    private bool IsSameCenter(IReadOnlyList<byte> tileIds)
    {
        bool result;
        if (tileIds == null)
        {
            result = shownCenterIds.Count == 0;
        }
        else if (tileIds.Count != shownCenterIds.Count)
        {
            result = false;
        }
        else
        {
            result = true;
            for (int i = 0; i < tileIds.Count; i++)
            {
                if (tileIds[i] != shownCenterIds[i])
                {
                    result = false;
                }
            }
        }
        return result;
    }

    // 이전 가운데 패는 날아가던 중이어도 바로 사라짐
    private void ReleaseCenter()
    {
        playFlights.Clear();
        for (int i = 0; i < centerViews.Count; i++)
        {
            ReleaseTile(centerViews[i]);
        }
        centerViews.Clear();
        shownCenterIds.Clear();
    }

    // ---------------- 낸 패 날아가기 연출 ----------------

    // 내가 낸 패는 손패에 있던 자리에서, 상대가 낸 패는 그 상대의 뒷면 타일 줄에서 출발
    // 손패 갱신(RPC)과 가운데 패 갱신(SyncVar)은 도착 순서가 정해져 있지 않아서 두 경우를 모두 처리
    private void StartPlayFlight(TileView view, int playSeat, int index, int count)
    {
        PlayFlight flight = new PlayFlight();
        flight.view = view;
        flight.endPosition = view.transform.position;
        flight.endRotation = view.transform.rotation;
        flight.startTime = index * playStagger;
        flight.isFlip = false;
        flight.isLanded = false;

        bool hasOrigin = false;
        byte tileId = view.Tile.Id;
        if (handPool.TryGetValue(tileId, out TileView handView) && handView.gameObject.activeSelf)
        {
            // 가운데 패가 먼저 온 경우: 아직 손패에 있는 타일을 숨기고 그 자리에서 출발 (손패에서는 곧 RPC로 빠짐)
            flight.startPosition = handView.transform.position;
            flight.startRotation = handView.transform.rotation;
            if (handView == draggingView || handView == pressedView)
            {
                CancelDrag();
                LayoutHand();
            }
            handView.gameObject.SetActive(false);
            hasOrigin = true;
        }
        else if (playOrigins.TryGetValue(tileId, out PlayOrigin origin))
        {
            // 손패가 먼저 온 경우: 빠질 때 기억해 둔 위치에서 출발
            if (Time.time - origin.time <= playOriginKeepSeconds)
            {
                flight.startPosition = origin.position;
                flight.startRotation = origin.rotation;
                hasOrigin = true;
            }
            playOrigins.Remove(tileId);
        }
        else if (TryGetBackTilePosition(playSeat, index, count, out Vector3 backPosition))
        {
            // 상대 패: 뒷면으로 날아오다가 도착하면서 앞면으로 뒤집힘
            flight.startPosition = backPosition;
            flight.startRotation = flight.endRotation * Quaternion.Euler(0f, 180f, 0f);
            flight.isFlip = true;
            hasOrigin = true;
        }

        if (hasOrigin)
        {
            view.transform.SetPositionAndRotation(flight.startPosition, flight.startRotation);
            playFlights.Add(flight);
        }
    }

    // 상대 자리 뒷면 타일 줄의 가운데쯤에서 count장 중 index번째 위치
    private bool TryGetBackTilePosition(int seat, int index, int count, out Vector3 position)
    {
        bool result = false;
        position = Vector3.zero;
        if (seat >= 0 && seat < hasSeatAnchor.Length && hasSeatAnchor[seat] && shownTileCounts[seat] > 0)
        {
            int rowCount = shownTileCounts[seat];
            int first = Mathf.Max(0, (rowCount - count) / 2);
            int rowIndex = Mathf.Min(first + index, rowCount - 1);
            int backIndex = seatBackStarts[seat] + rowIndex;
            if (backIndex < activeBackCount)
            {
                position = backPool[backIndex].transform.position;
                result = true;
            }
        }
        return result;
    }

    private void RememberPlayOrigin(TileView view)
    {
        if (view.gameObject.activeSelf)
        {
            PlayOrigin origin = new PlayOrigin();
            origin.position = view.transform.position;
            origin.rotation = view.transform.rotation;
            origin.time = Time.time;
            playOrigins[view.Tile.Id] = origin;
        }
    }

    private void UpdatePlay()
    {
        if (playFlights.Count > 0)
        {
            float elapsed = Time.time - playStartTime;
            float flightSeconds = Mathf.Max(0.01f, playFlightSeconds);
            bool isAllLanded = true;
            for (int i = 0; i < playFlights.Count; i++)
            {
                PlayFlight flight = playFlights[i];
                if (flight.isLanded == false)
                {
                    float t = (elapsed - flight.startTime) / flightSeconds;
                    if (t >= 1f)
                    {
                        flight.view.transform.rotation = flight.endRotation;
                        flight.view.SetBasePosition(flight.endPosition);
                        flight.isLanded = true;
                        playFlights[i] = flight;
                    }
                    else
                    {
                        isAllLanded = false;
                        if (t >= 0f)
                        {
                            MovePlayFlight(flight, t);
                        }
                    }
                }
            }

            if (isAllLanded)
            {
                playFlights.Clear();
            }
        }
    }

    private void MovePlayFlight(PlayFlight flight, float t)
    {
        float eased = 1f - (1f - t) * (1f - t);
        Vector3 position = Vector3.Lerp(flight.startPosition, flight.endPosition, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * playArcHeight);
        Quaternion rotation;
        if (flight.isFlip)
        {
            float flip = Mathf.SmoothStep(0f, 1f, (t - 0.4f) / 0.6f);
            rotation = flight.endRotation * Quaternion.Euler(0f, 180f * (1f - flip), 0f);
        }
        else
        {
            rotation = Quaternion.Slerp(flight.startRotation, flight.endRotation, eased);
        }
        flight.view.transform.SetPositionAndRotation(position, rotation);
    }

    // ---------------- 상대 뒷면 타일 ----------------

    private void RefreshOpponents(LexioTable table)
    {
        LexioPlayer local = networkManager.LocalPlayer;
        int mySeat = -1;
        int playerCount = 0;
        if (local != null && table.Phase != GamePhase.Waiting)
        {
            mySeat = local.Seat;
            playerCount = table.Seats.Count;
        }

        bool isChanged = mySeat != shownMySeat || playerCount != shownPlayerCount;
        for (int seat = 0; seat < playerCount; seat++)
        {
            if (table.Seats[seat].tileCount != shownTileCounts[seat])
            {
                isChanged = true;
            }
        }

        if (isChanged)
        {
            shownMySeat = mySeat;
            shownPlayerCount = playerCount;
            for (int seat = 0; seat < shownTileCounts.Length; seat++)
            {
                shownTileCounts[seat] = 0;
                hasSeatAnchor[seat] = false;
            }
            for (int seat = 0; seat < playerCount; seat++)
            {
                shownTileCounts[seat] = table.Seats[seat].tileCount;
            }

            activeBackCount = 0;
            if (playerCount > 0)
            {
                BuildOpponents(mySeat, playerCount);
            }
            HideUnusedBackTiles();
        }
    }

    private void BuildOpponents(int mySeat, int playerCount)
    {
        float spacing = GetTileWidth() * opponentSpacingRatio;
        float radiusScale = 1f;
        if (playerCount >= GameRules.MaxPlayers)
        {
            radiusScale = fivePlayerRadiusScale;
        }
        float radiusX = opponentRadiusX * radiusScale;
        float radiusZ = opponentRadiusZ * radiusScale;
        for (int seat = 0; seat < playerCount; seat++)
        {
            if (seat != mySeat)
            {
                // 내 자리(아래, -90도)에서 반시계 방향으로 차례대로 배치
                int relative = (seat - mySeat + playerCount) % playerCount;
                float angle = (-90f + 360f * relative / playerCount) * Mathf.Deg2Rad;
                Vector3 anchor = new Vector3(Mathf.Cos(angle) * radiusX, 0f, Mathf.Sin(angle) * radiusZ + opponentOffsetZ);
                seatAnchors[seat] = anchor + Vector3.up * labelHeight;
                hasSeatAnchor[seat] = true;

                Vector3 toCenter = new Vector3(-anchor.x, 0f, opponentOffsetZ - anchor.z).normalized;
                Quaternion facing = Quaternion.LookRotation(toCenter, Vector3.up);
                Vector3 rowDirection = facing * Vector3.right;
                Quaternion rotation = facing * Quaternion.Euler(backTileEuler);

                int count = shownTileCounts[seat];
                seatBackStarts[seat] = activeBackCount;
                float startOffset = -(count - 1) * spacing * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    GameObject back = AcquireBackTile();
                    if (back != null)
                    {
                        back.transform.rotation = rotation;
                        back.transform.position = anchor + rowDirection * (startOffset + i * spacing) + Vector3.up * opponentTileHeight;
                    }
                }
            }
        }
    }

    private GameObject AcquireBackTile()
    {
        GameObject back = null;
        if (activeBackCount < backPool.Count)
        {
            back = backPool[activeBackCount];
        }
        else if (tileDatabase != null && tileDatabase.BackPrefab != null)
        {
            back = Instantiate(tileDatabase.BackPrefab, transform);
            back.transform.localScale = Vector3.one * tileScale;
            backPool.Add(back);
        }

        if (back != null)
        {
            if (back.activeSelf == false)
            {
                back.SetActive(true);
            }
            activeBackCount++;
        }
        return back;
    }

    private void HideUnusedBackTiles()
    {
        for (int i = activeBackCount; i < backPool.Count; i++)
        {
            if (backPool[i].activeSelf)
            {
                backPool[i].SetActive(false);
            }
        }
    }

    // ---------------- 패 돌리기 연출 ----------------

    // 손패와 상대 뒷면 타일을 최종 위치에 미리 배치한 뒤 숨기고, 중앙에서 한 장씩 날려 보냄
    // 자리/인원은 동기화 값(local.Seat, table.Seats)이 아직 안 왔을 수 있어서 서버가 손패와 함께 보낸 값을 사용
    private void StartDeal(int mySeat, int playerCount)
    {
        if (playerCount >= GameRules.MinPlayers && playerCount <= GameRules.MaxPlayers && mySeat >= 0 && mySeat < playerCount)
        {
            int tilesPerPlayer = GameRules.GetTilesPerPlayer(playerCount);

            shownMySeat = mySeat;
            shownPlayerCount = playerCount;
            for (int seat = 0; seat < shownTileCounts.Length; seat++)
            {
                shownTileCounts[seat] = 0;
                hasSeatAnchor[seat] = false;
            }
            for (int seat = 0; seat < playerCount; seat++)
            {
                shownTileCounts[seat] = tilesPerPlayer;
            }
            activeBackCount = 0;
            BuildOpponents(mySeat, playerCount);
            HideUnusedBackTiles();

            int flightCount = tilesPerPlayer * playerCount;
            float interval = 0f;
            if (flightCount > 1)
            {
                interval = Mathf.Max(0f, dealSeconds - dealFlightSeconds) / (flightCount - 1);
            }

            // 실제 렉시오처럼 자리마다 돌아가며 한 장씩
            int order = 0;
            for (int lap = 0; lap < tilesPerPlayer; lap++)
            {
                for (int seat = 0; seat < playerCount; seat++)
                {
                    Transform target = null;
                    TileView handView = null;
                    if (seat == mySeat)
                    {
                        if (lap < handViews.Count)
                        {
                            handView = handViews[lap];
                            target = handView.transform;
                        }
                    }
                    else
                    {
                        int backIndex = seatBackStarts[seat] + lap;
                        if (backIndex < activeBackCount)
                        {
                            target = backPool[backIndex].transform;
                        }
                    }

                    if (target != null)
                    {
                        DealFlight flight = new DealFlight();
                        flight.target = target;
                        flight.handView = handView;
                        flight.endPosition = target.position;
                        flight.endRotation = target.rotation;
                        flight.startTime = order * interval;
                        flight.isLanded = false;
                        dealFlights.Add(flight);
                        target.gameObject.SetActive(false);
                    }
                    order++;
                }
            }

            dealStartTime = Time.time;
            isDealing = dealFlights.Count > 0;
        }
    }

    private void UpdateDeal()
    {
        float elapsed = Time.time - dealStartTime;
        float flightSeconds = Mathf.Max(0.01f, dealFlightSeconds);
        bool isAllLanded = true;
        for (int i = 0; i < dealFlights.Count; i++)
        {
            DealFlight flight = dealFlights[i];
            if (flight.isLanded == false)
            {
                float t = (elapsed - flight.startTime) / flightSeconds;
                if (t >= 1f)
                {
                    LandFlight(flight);
                    flight.isLanded = true;
                    dealFlights[i] = flight;
                }
                else
                {
                    isAllLanded = false;
                    if (t >= 0f)
                    {
                        MoveFlight(flight, t);
                    }
                }
            }
        }

        if (isAllLanded)
        {
            dealFlights.Clear();
            isDealing = false;
            cachedVersion = -1;
            NotifySelectionChanged();
        }
    }

    // 중앙에서 포물선으로 날아감. 내 패는 뒷면으로 날아오다가 도착하면서 앞면으로 뒤집힘
    private void MoveFlight(DealFlight flight, float t)
    {
        if (flight.target.gameObject.activeSelf == false)
        {
            flight.target.gameObject.SetActive(true);
        }

        float eased = 1f - (1f - t) * (1f - t);
        Vector3 position = Vector3.Lerp(dealOrigin, flight.endPosition, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * dealArcHeight);
        Quaternion rotation = flight.endRotation;
        if (flight.handView != null)
        {
            float flip = Mathf.SmoothStep(0f, 1f, (t - 0.4f) / 0.6f);
            rotation = flight.endRotation * Quaternion.Euler(0f, 180f * (1f - flip), 0f);
        }
        flight.target.SetPositionAndRotation(position, rotation);
    }

    private void LandFlight(DealFlight flight)
    {
        if (flight.target.gameObject.activeSelf == false)
        {
            flight.target.gameObject.SetActive(true);
        }

        flight.target.SetPositionAndRotation(flight.endPosition, flight.endRotation);
        if (flight.handView != null)
        {
            flight.handView.SetBasePosition(flight.endPosition);
        }
    }

    // 연출 중단 (새 패를 받았거나 접속 종료)
    private void ResetDeal()
    {
        dealFlights.Clear();
        isDealing = false;
    }

    // ---------------- 공통 ----------------

    private TileView AcquireTile(Dictionary<byte, TileView> pool, Tile tile, bool isClickable)
    {
        if (pool.TryGetValue(tile.Id, out TileView view))
        {
            view.gameObject.SetActive(true);
        }
        else
        {
            view = CreateTile(tile, isClickable);
            pool.Add(tile.Id, view);
        }
        return view;
    }

    private void ReleaseTile(TileView view)
    {
        if (view.IsSelected)
        {
            view.SetSelected(false);
        }
        view.gameObject.SetActive(false);
    }

    private TileView CreateTile(Tile tile, bool isClickable)
    {
        GameObject prefab = null;
        if (tileDatabase != null)
        {
            prefab = tileDatabase.GetPrefab(tile);
        }
        GameObject instance;
        if (prefab != null)
        {
            instance = Instantiate(prefab, transform);
        }
        else
        {
            Debug.LogWarning("타일 프리팹이 없습니다: " + tile);
            instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.transform.SetParent(transform);
        }
        instance.transform.localScale = Vector3.one * tileScale;

        TileView view = instance.AddComponent<TileView>();
        view.Initialize(tile, outlineMaterial, outlineCache, selectedLift, selectedTint, isClickable);
        return view;
    }

    // 타일 1장의 가로 폭 (프리팹 바운드로 한 번 측정)
    private float GetTileWidth()
    {
        MeasureTile();
        return tileSize.x;
    }

    // 타일 크기와 피벗→중심 오프셋 (칸 표시 위치/크기에 사용)
    private void MeasureTile()
    {
        if (isTileMeasured == false)
        {
            isTileMeasured = true;
            tileSize = Vector3.one * tileScale;
            tileCenterOffset = Vector3.zero;
            GameObject prefab = null;
            if (tileDatabase != null)
            {
                prefab = tileDatabase.GetPrefab(new Tile(Suit.Cloud, 3));
            }
            if (prefab != null)
            {
                GameObject probe = Instantiate(prefab);
                probe.transform.position = Vector3.zero;
                probe.transform.rotation = Quaternion.identity;
                probe.transform.localScale = Vector3.one * tileScale;
                Renderer[] probeRenderers = probe.GetComponentsInChildren<Renderer>();
                if (probeRenderers.Length > 0)
                {
                    Bounds bounds = probeRenderers[0].bounds;
                    for (int i = 1; i < probeRenderers.Length; i++)
                    {
                        bounds.Encapsulate(probeRenderers[i].bounds);
                    }
                    tileSize = bounds.size;
                    tileCenterOffset = bounds.center;
                }
                DestroyImmediate(probe);
            }
        }
    }

    private int GetMaxNumber()
    {
        int maxNumber = GameRules.GetMaxNumber(GameRules.MaxPlayers);
        LexioTable table = networkManager.Table;
        if (table != null && table.MaxNumber > 0)
        {
            maxNumber = table.MaxNumber;
        }
        return maxNumber;
    }

    private void NotifySelectionChanged()
    {
        if (SelectionChanged != null)
        {
            SelectionChanged();
        }
    }

    // 접속 종료: 오브젝트는 풀에 남겨두고 숨기기만 함
    private void ClearAll()
    {
        CancelDrag();
        ResetDeal();
        ReleaseHand();
        ReleaseCenter();
        playOrigins.Clear();
        activeBackCount = 0;
        HideUnusedBackTiles();
        shownMySeat = -1;
        shownPlayerCount = 0;
        cachedVersion = -1;
        for (int seat = 0; seat < shownTileCounts.Length; seat++)
        {
            shownTileCounts[seat] = 0;
            hasSeatAnchor[seat] = false;
        }
        NotifySelectionChanged();
    }
}
