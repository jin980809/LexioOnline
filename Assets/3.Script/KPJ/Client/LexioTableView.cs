using System.Collections.Generic;
using UnityEngine;

// 3D 테이블 표시의 연결 담당 (LexioClientView를 기능별 컴포넌트로 나눈 것)
// 네트워크 이벤트를 받아 각 컴포넌트에 나눠 주고, 매 프레임 순서대로 갱신함
// 타일 생성/풀링, 타일 크기 측정 같은 공통 기능도 여기서 제공
// LexioUI / ChipFlightView는 이 컴포넌트만 참조함
// 테이블은 XZ 평면, 내 자리는 -Z 쪽(화면 아래)
public class LexioTableView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LexioTableNetworkManager networkManager;
    [SerializeField] private TileDatabase tileDatabase;
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private HandView handView;
    [SerializeField] private OpponentView opponentView;
    [SerializeField] private CenterPlayView centerPlayView;
    [SerializeField] private DealAnimator dealAnimator;

    [Header("Tile")]
    [SerializeField] private float tileScale = 0.3f;
    [SerializeField] private float tileGap = 0.06f;
    [SerializeField] private float selectedLift = 0.35f;
    [SerializeField] private Color selectedTint = new Color(1f, 0.95f, 0.7f, 1f);

    private readonly Dictionary<Mesh, Material> outlineCache = new Dictionary<Mesh, Material>();
    private int cachedVersion = -1;
    private bool isTileMeasured;
    private Vector3 tileSize;
    private Vector3 tileCenterOffset;

    public event System.Action SelectionChanged
    {
        add { handView.SelectionChanged += value; }
        remove { handView.SelectionChanged -= value; }
    }

    // 패 돌리는 중에는 선택/정렬/제출 불가
    public bool IsDealing
    {
        get { return dealAnimator.IsDealing; }
    }

    public Camera ViewCamera
    {
        get { return viewCamera; }
    }

    public LexioTableNetworkManager NetworkManager
    {
        get { return networkManager; }
    }

    public float TileGap
    {
        get { return tileGap; }
    }

    public float SelectedLift
    {
        get { return selectedLift; }
    }

    // 타일 1장의 크기 (프리팹 바운드로 한 번 측정)
    public Vector3 TileSize
    {
        get
        {
            MeasureTile();
            return tileSize;
        }
    }

    // 타일 피벗 → 중심 오프셋 (칸 표시 위치에 사용)
    public Vector3 TileCenterOffset
    {
        get
        {
            MeasureTile();
            return tileCenterOffset;
        }
    }

    private void Awake()
    {
        // 참조가 비어 있으면 타일 대신 큐브가 표시됨 (예외로 서버 연결이 끊기지 않도록 미리 알림)
        if (tileDatabase == null)
        {
            Debug.LogError("[Lexio] LexioTableView에 TileDatabase가 연결되지 않았습니다. Assets/11.Data/TileDatabase.asset을 연결하세요.");
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
        if (dealAnimator.IsDealing)
        {
            dealAnimator.UpdateDeal();
        }
        else
        {
            handView.HandleInput();
        }
        centerPlayView.UpdatePlay();
        opponentView.UpdateReveal();

        LexioTable table = networkManager.Table;
        if (table != null && table.Version != cachedVersion)
        {
            cachedVersion = table.Version;
            centerPlayView.RefreshCenter(table);
            // 패 돌리는 중에는 상대 타일을 연출이 직접 보여줌 (끝나면 RequestRefresh로 다시 갱신)
            if (dealAnimator.IsDealing == false)
            {
                opponentView.RefreshOpponents(table);
            }
        }
    }

    // ---------------- LexioUI / ChipFlightView용 ----------------

    // 선택된 타일을 results에 채움 (매번 새 리스트를 만들지 않도록 호출 측 리스트 재사용)
    public void GetSelectedTiles(List<Tile> results)
    {
        handView.GetSelectedTiles(results);
    }

    public void ClearSelection()
    {
        handView.ClearSelection();
    }

    public void SortHand(HandSortMode mode)
    {
        if (dealAnimator.IsDealing == false)
        {
            handView.SortHand(mode);
        }
    }

    // 상대 자리 라벨을 띄울 월드 좌표
    public bool TryGetSeatAnchor(int seat, out Vector3 position)
    {
        return opponentView.TryGetSeatAnchor(seat, out position);
    }

    // 정산 칩이 출발/도착하는 자리 (테이블 면 높이, 패 줄 앞쪽)
    public bool TryGetChipAnchor(int seat, out Vector3 position)
    {
        return opponentView.TryGetChipAnchor(seat, out position);
    }

    // ---------------- 네트워크 이벤트 ----------------

    private void OnHandReceived(byte[] tileIds, bool isNewDeal, int mySeat, int playerCount)
    {
        if (isNewDeal)
        {
            // 새 라운드: 진행 중이던 연출과 지난 라운드 출발 위치를 정리
            dealAnimator.ResetDeal();
            centerPlayView.ClearPlayOrigins();
        }
        handView.ApplyHand(tileIds, isNewDeal);
        if (isNewDeal)
        {
            dealAnimator.StartDeal(mySeat, playerCount);
        }
        handView.NotifySelectionChanged();
    }

    private void OnHandsRevealed(byte[] tileIds, int[] counts)
    {
        opponentView.OnHandsRevealed(tileIds, counts);
    }

    // 접속 종료: 오브젝트는 풀에 남겨두고 숨기기만 함
    private void ClearAll()
    {
        dealAnimator.ResetDeal();
        handView.ClearAll();
        centerPlayView.ClearAll();
        opponentView.ClearAll();
        cachedVersion = -1;
        handView.NotifySelectionChanged();
    }

    // ---------------- 공통 ----------------

    // 다음 프레임에 테이블 상태로 가운데 패/상대 배치를 다시 갱신
    public void RequestRefresh()
    {
        cachedVersion = -1;
    }

    public TileView AcquireTile(Dictionary<byte, TileView> pool, Tile tile, bool isClickable)
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

    public void ReleaseTile(TileView view)
    {
        if (view.IsSelected)
        {
            view.SetSelected(false);
        }
        view.gameObject.SetActive(false);
    }

    // 상대 뒷면 타일 1장 생성 (프리팹이 없으면 null)
    public GameObject CreateBackTile()
    {
        GameObject back = null;
        if (tileDatabase != null && tileDatabase.BackPrefab != null)
        {
            back = Instantiate(tileDatabase.BackPrefab, transform);
            back.transform.localScale = Vector3.one * tileScale;
        }
        return back;
    }

    public float GetTileWidth()
    {
        return TileSize.x;
    }

    public int GetMaxNumber()
    {
        int maxNumber = GameRules.GetMaxNumber(GameRules.MaxPlayers);
        LexioTable table = networkManager.Table;
        if (table != null && table.MaxNumber > 0)
        {
            maxNumber = table.MaxNumber;
        }
        return maxNumber;
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

    // 타일 크기와 피벗→중심 오프셋을 프리팹 하나로 한 번만 측정
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
}
