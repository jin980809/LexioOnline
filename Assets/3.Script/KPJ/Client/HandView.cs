using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 내 손패: 받은 패 배치, 클릭 선택, 정렬, 칸 드래그(선택한 패 묶음 이동)
// 손패 칸(handSlots)이 위치를 결정하고, handViews는 손에 든 타일 목록
// 입력 처리는 LexioTableView가 패 돌리는 중이 아닐 때만 HandleInput으로 부름
public class HandView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LexioTableView tableView;
    // 손패에서 빠지는 타일 위치를 낸 패 날아가기 출발점으로 넘겨줌
    [SerializeField] private CenterPlayView centerPlayView;

    [Header("Hand (내 패)")]
    [SerializeField] private Vector3 handCenter = new Vector3(1.3f, 0.55f, -6.3f);
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
    [SerializeField] private Color slotColor = new Color(1f, 0.97f, 0.88f, 0.22f);
    [SerializeField] private Color slotHighlightColor = new Color(1f, 0.85f, 0.4f, 0.75f);

    private readonly List<TileView> handViews = new List<TileView>();
    private readonly Dictionary<byte, TileView> handPool = new Dictionary<byte, TileView>();
    private readonly List<Tile> receivedTiles = new List<Tile>();
    private Comparison<TileView> suitFirstComparison;
    private Comparison<TileView> numberFirstComparison;
    private int sortMaxNumber;

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
    // 내 패를 클릭해서 선택했을 때 (해제·끌어 옮기기·선택 초기화는 제외). 효과음용
    public event Action TileSelected;

    // 손에 든 타일 (패 돌리기 연출이 받은 순서대로 날려 보낼 때 사용)
    public IReadOnlyList<TileView> Tiles
    {
        get { return handViews; }
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
        dragPreviewSlots = new TileView[handSlots.Length];
        slotPropertyBlock = new MaterialPropertyBlock();
        slotColorId = Shader.PropertyToID("_BaseColor");

        if (slotMaterial == null)
        {
            Debug.LogWarning("[Lexio] HandView에 slotMaterial이 없어 드래그 중 칸이 표시되지 않습니다. Lexio/Setup Project를 다시 실행하세요.");
        }
    }

    // ---------------- 손패 ----------------

    // 서버가 보낸 손패로 갱신. 새 라운드면 받은 순서대로 놓고, 아니면 낸 패만 빼고 현재 배치를 유지
    public void ApplyHand(byte[] tileIds, bool isNewDeal)
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
            ReleaseHand();
            for (int i = 0; i < receivedTiles.Count; i++)
            {
                handViews.Add(tableView.AcquireTile(handPool, receivedTiles[i], true));
            }
            FillSlotsCentered();
        }
        else
        {
            for (int i = handViews.Count - 1; i >= 0; i--)
            {
                if (receivedTiles.Contains(handViews[i].Tile) == false)
                {
                    centerPlayView.RememberPlayOrigin(handViews[i]);
                    RemoveFromSlot(handViews[i]);
                    tableView.ReleaseTile(handViews[i]);
                    handViews.RemoveAt(i);
                }
            }

            for (int i = 0; i < receivedTiles.Count; i++)
            {
                if (IsInHand(receivedTiles[i]) == false)
                {
                    TileView view = tableView.AcquireTile(handPool, receivedTiles[i], true);
                    handViews.Add(view);
                    PutInFirstEmptySlot(view);
                }
            }
        }

        LayoutHand();
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
        sortMaxNumber = tableView.GetMaxNumber();
        if (mode == HandSortMode.SuitFirst)
        {
            handViews.Sort(suitFirstComparison);
        }
        else
        {
            handViews.Sort(numberFirstComparison);
        }
        FillSlotsCentered();
        LayoutHand();
    }

    // 아직 손패에 보이는 타일 (가운데 패가 손패 갱신보다 먼저 온 경우 출발 위치로 사용)
    public bool TryGetShownTile(byte tileId, out TileView view)
    {
        bool result = handPool.TryGetValue(tileId, out view) && view.gameObject.activeSelf;
        return result;
    }

    // 손패에서 곧 빠질 타일을 끌거나 누르고 있었으면 입력을 취소하고 제자리로 되돌림
    public void ReleaseFromInput(TileView view)
    {
        if (dragGroup.Contains(view) || view == pressedView)
        {
            CancelDrag();
            LayoutHand();
        }
    }

    // 내 칩 자리: 손패 맨 뒷줄에서 테이블 가운데 쪽으로 frontGap만큼
    public Vector3 GetChipAnchor(float frontGap)
    {
        float backRowZ = handCenter.z + GetRowOffset().z * (handSlotRows - 1);
        return new Vector3(handCenter.x, 0f, backRowZ + frontGap);
    }

    public void NotifySelectionChanged()
    {
        if (SelectionChanged != null)
        {
            SelectionChanged();
        }
    }

    // 접속 종료: 오브젝트는 풀에 남겨두고 숨기기만 함
    public void ClearAll()
    {
        CancelDrag();
        ReleaseHand();
    }

    // 짧게 클릭하면 선택/해제, 누른 채 일정 거리 이상 끌면 칸 이동
    public void HandleInput()
    {
        Mouse mouse = Mouse.current;
        Camera viewCamera = tableView.ViewCamera;
        if (mouse != null && viewCamera != null)
        {
            Vector2 screenPosition = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressedView = FindHandTileAt(viewCamera, screenPosition);
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
                    UpdateDrag(viewCamera, screenPosition);
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
                    if (pressedView.IsSelected && TileSelected != null)
                    {
                        TileSelected();
                    }
                }
                pressedView = null;
            }
        }
    }

    private TileView FindHandTileAt(Camera viewCamera, Vector2 screenPosition)
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
        return tableView.GetTileWidth() + tableView.TileGap;
    }

    // 한 줄 뒤로 갈 때 이동량. 뒤로(+Z) = 타일 두께 + 앞줄 타일이 뒤로 기운 만큼 + 간격, 위로(+Y) = 세로 길이 * handRowRise
    private Vector3 GetRowOffset()
    {
        Vector3 tileSize = tableView.TileSize;
        float lean = Mathf.Abs(Mathf.Sin(handTileEuler.x * Mathf.Deg2Rad)) * tileSize.y;
        float depth = tileSize.z + lean + handRowGap;
        float rise = tileSize.y * handRowRise;
        return new Vector3(0f, rise, depth);
    }

    // 정렬/새 라운드: handViews 순서대로 앞줄 가운데에 모아 채움 (양쪽 빈칸 수를 맞춤, 홀수면 오른쪽이 한 칸 더 빔)
    // 앞줄보다 많으면 앞줄을 다 채우고 나머지는 뒷줄 가운데부터
    private void FillSlotsCentered()
    {
        Array.Clear(handSlots, 0, handSlots.Length);
        int index = 0;
        for (int row = 0; row < handSlotRows && index < handViews.Count; row++)
        {
            int countInRow = Mathf.Min(handSlotColumns, handViews.Count - index);
            int start = row * handSlotColumns + (handSlotColumns - countInRow) / 2;
            for (int i = 0; i < countInRow; i++)
            {
                handSlots[start + i] = handViews[index];
                index++;
            }
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
    private void UpdateDrag(Camera viewCamera, Vector2 screenPosition)
    {
        int slot = FindNearestSlotOnScreen(viewCamera, screenPosition);
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
                    goal += Vector3.up * tableView.SelectedLift;
                }
                view.transform.position = Vector3.Lerp(view.transform.position, goal, t);
            }
        }
    }

    // 줄이 앞뒤로 겹쳐 보이므로 3D 면 대신 화면 좌표로 칸 중심과의 거리를 비교
    private int FindNearestSlotOnScreen(Camera viewCamera, Vector2 screenPosition)
    {
        Vector3 centerOffset = Quaternion.Euler(handTileEuler) * tableView.TileCenterOffset;
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
            Vector3 tileSize = tableView.TileSize;
            Quaternion rotation = Quaternion.Euler(handTileEuler);
            Vector3 centerOffset = rotation * tableView.TileCenterOffset;
            // 옆 칸 표시와 겹치면 반투명이 겹쳐 진하게 보이므로, 너비는 칸 간격보다 조금 좁게 제한함
            float markerWidth = Mathf.Min(tileSize.x * 1.12f, GetSlotSpacing() - 0.01f);
            Vector3 markerScale = new Vector3(markerWidth, tileSize.y * 1.06f, 1f);
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
            tableView.ReleaseTile(handViews[i]);
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
}
