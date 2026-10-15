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
    [SerializeField] private LexioTableNetworkManager networkManager;
    [SerializeField] private TileDatabase tileDatabase;
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Camera viewCamera;

    [Header("Tile")]
    [SerializeField] private float tileScale = 0.3f;
    [SerializeField] private float tileGap = 0.06f;
    [SerializeField] private float selectedLift = 0.35f;
    [SerializeField] private Color selectedTint = new Color(1f, 0.95f, 0.7f, 1f);

    [Header("Hand (내 패)")]
    [SerializeField] private Vector3 handCenter = new Vector3(1.2f, 0.55f, -5f);
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
    // 드래그 중 패가 칸 사이를 미끄러지는 빠르기 (클수록 빨리 붙음, 30이면 약 0.08초에 거의 도착)
    [SerializeField] private float dragSlideSpeed = 30f;
    [SerializeField] private Material slotMaterial;
    [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color slotHighlightColor = new Color(1f, 0.85f, 0.2f, 0.45f);

    [Header("Center (마지막 낸 패)")]
    [SerializeField] private Vector3 centerPosition = new Vector3(0f, 0.25f, 0f);
    [SerializeField] private Vector3 centerTileEuler = new Vector3(90f, 0f, 0f);

    [Header("Opponents (상대)")]
    [SerializeField] private float opponentRadiusX = 5f;
    [SerializeField] private float opponentRadiusZ = 3.6f;
    [SerializeField] private float opponentOffsetZ = 1.2f;
    // 5인(상대 4명)일 때 자리끼리 줄 끝이 붙지 않도록 배치 타원을 키우는 배율
    [SerializeField] private float fivePlayerRadiusScale = 1.3f;
    [SerializeField] private float opponentTileHeight = 0.55f;
    [SerializeField] private float opponentSpacingRatio = 1.1f;
    // 상대 패를 놓는 줄 수. 앞줄(테이블 가운데 쪽)부터 채우고 다음 줄은 바깥쪽으로 물러남 (공개 패도 같은 줄 수)
    [SerializeField, Min(1)] private int opponentRows = 2;
    [SerializeField] private float opponentRowGap = 0.08f;
    // 뒷줄을 앞줄 너머로 보이게 올리는 높이 (타일 세로 길이 대비 비율). 공개 패는 눕혀 놓으므로 올리지 않음
    [SerializeField] private float opponentRowRise = 0.4f;
    [SerializeField] private Vector3 backTileEuler = Vector3.zero;
    [SerializeField] private float labelHeight = 1.4f;
    // 상대 정보(이름표)를 패 줄보다 바깥쪽(테이블 가장자리 쪽)으로 물리는 거리. 뒷줄과 공개 패 뒷줄까지 피하도록 잡음
    [SerializeField] private float labelBackDistance = 3f;

    [Header("Chips (정산 칩이 출발/도착하는 자리)")]
    // 손패/상대 패 줄에서 테이블 가운데 쪽으로 떨어진 거리
    [SerializeField] private float chipFrontGap = 0.8f;

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

    [Header("Reveal (라운드 끝 상대 남은 패 공개)")]
    // 마지막 패가 가운데로 날아간 뒤에 공개 (바로 공개하면 마지막 패의 출발 위치인 뒷면 타일이 먼저 사라짐)
    [SerializeField] private float revealDelaySeconds = 0.5f;
    // 공개된 타일 간격 (타일 폭 대비). 1보다 작으면 조금씩 겹침
    [SerializeField] private float revealSpacingRatio = 1.1f;
    // 바닥에 눕힌 타일 높이
    [SerializeField] private float revealHeight = 0.25f;
    [SerializeField] private float revealFlipSeconds = 0.3f;
    [SerializeField] private float revealStagger = 0.05f;
    // 뒤집는 동안 살짝 들어 올림 (눕힌 채로 돌리면 모서리가 바닥에 묻힘)
    [SerializeField] private float revealFlipLift = 0.3f;

    // 뒷면에서 앞면으로 뒤집히며 공개되는 타일 1장
    private struct RevealTile
    {
        public TileView view;
        public Vector3 position;
        public Quaternion endRotation;
        public float startTime;
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
    private readonly Vector3[] chipAnchors = new Vector3[GameRules.MaxPlayers];
    private readonly bool[] hasChipAnchor = new bool[GameRules.MaxPlayers];
    private readonly List<DealFlight> dealFlights = new List<DealFlight>();
    private readonly List<PlayFlight> playFlights = new List<PlayFlight>();
    private readonly Dictionary<byte, PlayOrigin> playOrigins = new Dictionary<byte, PlayOrigin>();
    private readonly Dictionary<byte, TileView> revealPool = new Dictionary<byte, TileView>();
    private readonly List<RevealTile> revealTiles = new List<RevealTile>();
    // 자리별 공개된 남은 패 (Awake에서 생성)
    private readonly List<Tile>[] revealedHands = new List<Tile>[GameRules.MaxPlayers];
    private bool isRevealPending;
    private bool isRevealed;
    private bool isRevealDirty;
    private bool isRevealFlipping;
    private float revealApplyTime;
    private float revealStartTime;
    private Comparison<Tile> revealComparison;
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
    // 마우스로 잡은 패. 선택된 패를 잡으면 선택된 패 전체(dragGroup)가 함께 움직임
    private TileView draggingView;
    // 함께 옮기는 패와 원래 칸 (칸 번호 순서)
    private readonly List<TileView> dragGroup = new List<TileView>();
    private readonly List<int> dragOriginSlots = new List<int>();
    private readonly List<TileView> dragDisplaced = new List<TileView>();
    // 지금 놓으면 될 배치 (드래그 중 미리 보기용, handSlots와 같은 크기)
    private TileView[] dragPreviewSlots;
    // 잡은 패가 묶음에서 몇 번째인지
    private int dragGrabIndex;
    // 묶음이 들어갈 첫 칸 (여기서부터 dragGroup.Count칸)
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
        revealComparison = CompareRevealTile;
        for (int seat = 0; seat < revealedHands.Length; seat++)
        {
            revealedHands[seat] = new List<Tile>();
        }

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
        dragPreviewSlots = new TileView[handSlots.Length];
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
        networkManager.HandsRevealed += OnHandsRevealed;
        networkManager.ClientDisconnected += ClearAll;
    }

    private void OnDisable()
    {
        networkManager.HandReceived -= OnHandReceived;
        networkManager.HandsRevealed -= OnHandsRevealed;
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
        UpdateReveal();

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

    // 정산 칩이 출발/도착하는 자리 (테이블 면 높이, 패 줄 앞쪽)
    public bool TryGetChipAnchor(int seat, out Vector3 position)
    {
        bool result = false;
        position = Vector3.zero;
        if (seat >= 0 && seat < hasChipAnchor.Length && hasChipAnchor[seat])
        {
            position = chipAnchors[seat];
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

    // 선택된 패를 잡으면 선택된 패 전체를, 아니면 잡은 패 한 장만 옮김
    private void BeginDrag(TileView view)
    {
        draggingView = view;
        dragGroup.Clear();
        dragOriginSlots.Clear();
        for (int slot = 0; slot < handSlots.Length; slot++)
        {
            TileView slotView = handSlots[slot];
            bool isMember = slotView == view || (view.IsSelected && slotView != null && slotView.IsSelected);
            if (isMember)
            {
                dragGroup.Add(slotView);
                dragOriginSlots.Add(slot);
            }
        }
        dragGrabIndex = dragGroup.IndexOf(view);
        dragTargetSlot = GetGroupStartSlot(FindSlot(view));
        BuildDropArrangement(dragTargetSlot);
        ShowSlotMarkers(true);
        RefreshSlotHighlight();
    }

    // 잡은 패가 slot에 올 때 묶음의 첫 칸. 묶음이 줄 끝을 넘지 않도록 같은 줄 안으로 당김
    private int GetGroupStartSlot(int slot)
    {
        int row = slot / handSlotColumns;
        int column = slot % handSlotColumns - dragGrabIndex;
        column = Mathf.Clamp(column, 0, Mathf.Max(0, handSlotColumns - dragGroup.Count));
        return row * handSlotColumns + column;
    }

    // 마우스와 화면상 가장 가까운 칸을 놓을 칸으로 정하고, 놓았을 때의 배치를 미리 보여줌
    // 끄는 패는 칸 단위로 붙고, 밀려날 패는 갈 자리로 미리 옮겨 가서 패끼리 겹치지 않음
    private void UpdateDrag(Vector2 screenPosition)
    {
        int slot = FindNearestSlotOnScreen(screenPosition);
        int startSlot = GetGroupStartSlot(slot);
        if (startSlot != dragTargetSlot)
        {
            dragTargetSlot = startSlot;
            BuildDropArrangement(dragTargetSlot);
            RefreshSlotHighlight();
        }
        SlideToPreview();
    }

    // handSlots는 그대로 두고, 묶음을 targetSlot부터 붙여 넣은 결과를 dragPreviewSlots에 만듦
    // 묶음이 들어갈 칸에 있던 다른 패는 묶음이 떠난 칸으로 순서대로 옮겨 감
    // 묶음이 떠난 칸 중 비어 있는 칸은 항상 밀려난 패 수 이상이라 모두 들어감
    private void BuildDropArrangement(int targetSlot)
    {
        Array.Copy(handSlots, dragPreviewSlots, handSlots.Length);
        dragDisplaced.Clear();
        for (int i = 0; i < dragGroup.Count; i++)
        {
            TileView other = handSlots[targetSlot + i];
            if (other != null && dragGroup.Contains(other) == false)
            {
                dragDisplaced.Add(other);
            }
        }

        for (int i = 0; i < dragOriginSlots.Count; i++)
        {
            dragPreviewSlots[dragOriginSlots[i]] = null;
        }
        for (int i = 0; i < dragGroup.Count; i++)
        {
            dragPreviewSlots[targetSlot + i] = dragGroup[i];
        }

        int next = 0;
        for (int i = 0; i < dragOriginSlots.Count; i++)
        {
            int originSlot = dragOriginSlots[i];
            if (dragPreviewSlots[originSlot] == null && next < dragDisplaced.Count)
            {
                dragPreviewSlots[originSlot] = dragDisplaced[next];
                next++;
            }
        }
        dragDisplaced.Clear();
    }

    // 미리 보기 배치 위치로 매 프레임 조금씩 다가가서 부드럽게 미끄러짐
    // 끄는 패는 선택돼 있어도 올리지 않음 (놓으면 LayoutHand가 선택 높이로 되돌림)
    private void SlideToPreview()
    {
        float t = 1f - Mathf.Exp(-dragSlideSpeed * Time.deltaTime);
        for (int slot = 0; slot < dragPreviewSlots.Length; slot++)
        {
            TileView view = dragPreviewSlots[slot];
            if (view != null)
            {
                Vector3 goal = GetSlotPosition(slot);
                if (view.IsSelected && dragGroup.Contains(view) == false)
                {
                    goal += Vector3.up * selectedLift;
                }
                view.transform.position = Vector3.Lerp(view.transform.position, goal, t);
            }
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

    // 미리 보여준 배치를 그대로 확정. 묶음은 놓은 칸부터 붙여서 들어감
    // 한 장이면 기존과 같음: 다른 패가 있으면 서로 자리를 바꾸고, 빈칸이면 원래 칸이 빈칸이 됨
    // 선택 상태는 그대로 유지 (옮긴 뒤 바로 제출 가능)
    private void EndDrag()
    {
        if (dragTargetSlot >= 0 && dragGroup.Count > 0)
        {
            BuildDropArrangement(dragTargetSlot);
            Array.Copy(dragPreviewSlots, handSlots, handSlots.Length);
        }
        ClearDragState();
        ShowSlotMarkers(false);
        LayoutHand();
    }

    // 드래그 중 손패가 바뀌거나 접속이 끊긴 경우. 위치는 호출 측의 LayoutHand가 되돌림
    private void CancelDrag()
    {
        pressedView = null;
        ClearDragState();
        ShowSlotMarkers(false);
    }

    private void ClearDragState()
    {
        draggingView = null;
        dragGroup.Clear();
        dragOriginSlots.Clear();
        dragGrabIndex = 0;
        dragTargetSlot = -1;
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
                bool isTarget = dragTargetSlot >= 0 && slot >= dragTargetSlot && slot < dragTargetSlot + dragGroup.Count;
                if (isTarget)
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
            if (dragGroup.Contains(handView) || handView == pressedView)
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
            // 앞줄(테이블 가운데 쪽)의 가운데에서 꺼냄
            int frontCount = Mathf.Min(rowCount, GetOpponentRowLength(rowCount));
            int first = Mathf.Max(0, (frontCount - count) / 2);
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

        bool isChanged = isRevealDirty || mySeat != shownMySeat || playerCount != shownPlayerCount;
        for (int seat = 0; seat < playerCount; seat++)
        {
            if (table.Seats[seat].tileCount != shownTileCounts[seat])
            {
                isChanged = true;
            }
        }

        if (isChanged)
        {
            isRevealDirty = false;
            shownMySeat = mySeat;
            shownPlayerCount = playerCount;
            for (int seat = 0; seat < shownTileCounts.Length; seat++)
            {
                shownTileCounts[seat] = 0;
                hasSeatAnchor[seat] = false;
                hasChipAnchor[seat] = false;
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
            else
            {
                // 대기실로 돌아감: 공개했던 패도 정리
                ClearReveal();
            }
            HideUnusedBackTiles();
        }
    }

    private void BuildOpponents(int mySeat, int playerCount)
    {
        // 공개 타일은 자리 배치를 다시 할 때마다 새로 놓음 (뒤집는 진행 정도는 시간으로 계산하므로 이어짐)
        ReleaseRevealTiles();
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
            if (seat == mySeat)
            {
                SetMyChipAnchor(seat);
            }
            else
            {
                // 내 자리(아래, -90도)에서 반시계 방향으로 차례대로 배치
                int relative = (seat - mySeat + playerCount) % playerCount;
                float angle = (-90f + 360f * relative / playerCount) * Mathf.Deg2Rad;
                Vector3 anchor = new Vector3(Mathf.Cos(angle) * radiusX, 0f, Mathf.Sin(angle) * radiusZ + opponentOffsetZ);
                Vector3 toCenter = new Vector3(-anchor.x, 0f, opponentOffsetZ - anchor.z).normalized;
                seatAnchors[seat] = anchor - toCenter * labelBackDistance + Vector3.up * labelHeight;
                hasSeatAnchor[seat] = true;

                Quaternion facing = Quaternion.LookRotation(toCenter, Vector3.up);
                Vector3 rowDirection = facing * Vector3.right;
                Quaternion rotation = facing * Quaternion.Euler(backTileEuler);

                chipAnchors[seat] = anchor + toCenter * chipFrontGap;
                hasChipAnchor[seat] = true;

                seatBackStarts[seat] = activeBackCount;
                if (isRevealed)
                {
                    // 라운드 끝: 뒷면 대신 공개된 남은 패를 바닥에 눕혀 놓음
                    PlaceRevealedHand(seat, anchor, toCenter, rowDirection);
                }
                else
                {
                    // 세워 놓은 타일이라 줄 사이는 타일 두께만큼 띄우고, 뒷줄은 올려서 앞줄 너머로 보이게 함
                    int count = shownTileCounts[seat];
                    int rowLength = GetOpponentRowLength(count);
                    float rowDepth = tileSize.z + opponentRowGap;
                    float rowRise = tileSize.y * opponentRowRise;
                    for (int i = 0; i < count; i++)
                    {
                        GameObject back = AcquireBackTile();
                        if (back != null)
                        {
                            int row = i / rowLength;
                            back.transform.rotation = rotation;
                            back.transform.position = anchor + GetOpponentRowOffset(i, count, spacing, rowDepth, toCenter, rowDirection) + Vector3.up * (opponentTileHeight + rowRise * row);
                        }
                    }
                }
            }
        }
    }

    // count장을 opponentRows줄로 나눌 때 한 줄에 놓는 최대 장수 (앞줄부터 채움)
    private int GetOpponentRowLength(int count)
    {
        int rows = Mathf.Max(1, opponentRows);
        return Mathf.Max(1, Mathf.CeilToInt(count / (float)rows));
    }

    // count장 중 index번째 타일이 자리 기준점에서 떨어진 거리. 줄마다 가운데 정렬하고 뒷줄은 rowDepth씩 바깥으로 물러남
    private Vector3 GetOpponentRowOffset(int index, int count, float spacing, float rowDepth, Vector3 toCenter, Vector3 rowDirection)
    {
        int rowLength = GetOpponentRowLength(count);
        int row = index / rowLength;
        int column = index % rowLength;
        int countInRow = Mathf.Min(rowLength, count - row * rowLength);
        float startOffset = -(countInRow - 1) * spacing * 0.5f;
        return rowDirection * (startOffset + column * spacing) - toCenter * (rowDepth * row);
    }

    // ---------------- 라운드 끝 남은 패 공개 ----------------

    private void OnHandsRevealed(byte[] tileIds, int[] counts)
    {
        ClearReveal();
        if (tileIds != null && counts != null)
        {
            sortMaxNumber = GetMaxNumber();
            int index = 0;
            for (int seat = 0; seat < counts.Length && seat < revealedHands.Length; seat++)
            {
                for (int i = 0; i < counts[seat] && index < tileIds.Length; i++)
                {
                    revealedHands[seat].Add(new Tile(tileIds[index]));
                    index++;
                }
                revealedHands[seat].Sort(revealComparison);
            }
            isRevealPending = true;
            revealApplyTime = Time.time + revealDelaySeconds;
        }
    }

    // 상대 한 명의 남은 패를 그 자리 줄에 앞면이 위로 오게 눕힘. 타일 위쪽은 그 상대 쪽(바깥)을 향함
    private void PlaceRevealedHand(int seat, Vector3 anchor, Vector3 toCenter, Vector3 rowDirection)
    {
        List<Tile> hand = revealedHands[seat];
        float spacing = GetTileWidth() * revealSpacingRatio;
        // 눕혀 놓은 타일이라 줄 사이는 타일 세로 길이만큼 띄움
        float rowDepth = tileSize.y + opponentRowGap;
        Quaternion endRotation = Quaternion.LookRotation(Vector3.down, -toCenter);
        float elapsed = Time.time - revealStartTime;
        for (int i = 0; i < hand.Count; i++)
        {
            RevealTile reveal = new RevealTile();
            reveal.view = AcquireTile(revealPool, hand[i], false);
            reveal.position = anchor + GetOpponentRowOffset(i, hand.Count, spacing, rowDepth, toCenter, rowDirection) + Vector3.up * revealHeight;
            reveal.endRotation = endRotation;
            reveal.startTime = revealTiles.Count * revealStagger;
            revealTiles.Add(reveal);
            ApplyRevealTile(reveal, elapsed);
        }
    }

    private void UpdateReveal()
    {
        if (isRevealPending && Time.time >= revealApplyTime)
        {
            // 다음 상대 배치 갱신 때 뒷면 타일 대신 공개 타일을 놓음
            isRevealPending = false;
            isRevealed = true;
            isRevealDirty = true;
            isRevealFlipping = true;
            revealStartTime = Time.time;
            cachedVersion = -1;
        }

        if (isRevealFlipping && revealTiles.Count > 0)
        {
            float elapsed = Time.time - revealStartTime;
            bool isAllFlipped = true;
            for (int i = 0; i < revealTiles.Count; i++)
            {
                if (ApplyRevealTile(revealTiles[i], elapsed) == false)
                {
                    isAllFlipped = false;
                }
            }

            if (isAllFlipped)
            {
                isRevealFlipping = false;
            }
        }
    }

    // 뒷면이 위인 상태에서 타일 세로축으로 반 바퀴 돌아 앞면이 위로 옴. 다 뒤집혔으면 true
    private bool ApplyRevealTile(RevealTile reveal, float elapsed)
    {
        float t = Mathf.Clamp01((elapsed - reveal.startTime) / Mathf.Max(0.01f, revealFlipSeconds));
        float flip = Mathf.SmoothStep(0f, 1f, t);
        Quaternion rotation = reveal.endRotation * Quaternion.Euler(0f, 180f * (1f - flip), 0f);
        Vector3 position = reveal.position + Vector3.up * (Mathf.Sin(flip * Mathf.PI) * revealFlipLift);
        reveal.view.transform.rotation = rotation;
        reveal.view.SetBasePosition(position);
        return t >= 1f;
    }

    private int CompareRevealTile(Tile a, Tile b)
    {
        return a.GetPower(sortMaxNumber).CompareTo(b.GetPower(sortMaxNumber));
    }

    // 놓여 있는 공개 타일만 숨김 (공개된 패 정보는 유지)
    private void ReleaseRevealTiles()
    {
        for (int i = 0; i < revealTiles.Count; i++)
        {
            ReleaseTile(revealTiles[i].view);
        }
        revealTiles.Clear();
    }

    // 새 라운드 / 접속 종료: 공개 상태를 모두 비움
    private void ClearReveal()
    {
        ReleaseRevealTiles();
        for (int seat = 0; seat < revealedHands.Length; seat++)
        {
            revealedHands[seat].Clear();
        }
        isRevealPending = false;
        isRevealed = false;
        isRevealDirty = false;
        isRevealFlipping = false;
    }

    // 내 칩 자리: 손패 맨 뒷줄에서 테이블 가운데 쪽
    private void SetMyChipAnchor(int seat)
    {
        float backRowZ = handCenter.z + GetRowOffset().z * (handSlotRows - 1);
        chipAnchors[seat] = new Vector3(handCenter.x, 0f, backRowZ + chipFrontGap);
        hasChipAnchor[seat] = true;
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

            // 지난 라운드에 공개했던 패를 치우고 새로 돌림
            ClearReveal();
            shownMySeat = mySeat;
            shownPlayerCount = playerCount;
            for (int seat = 0; seat < shownTileCounts.Length; seat++)
            {
                shownTileCounts[seat] = 0;
                hasSeatAnchor[seat] = false;
                hasChipAnchor[seat] = false;
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
                prefab = tileDatabase.GetPrefab(new Tile(TileSuit.Cloud, 3));
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
        ClearReveal();
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
            hasChipAnchor[seat] = false;
        }
        NotifySelectionChanged();
    }
}
