using System;
using System.Collections.Generic;
using UnityEngine;

// 상대 자리 배치: 뒷면 타일 줄(2줄), 이름표/정산 칩 위치, 라운드 끝 남은 패 공개
// 상대 자리는 테이블 가운데 둘레의 타원 위에 놓고, 패 줄은 테이블 가운데를 바라보게 세움
public class OpponentView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LexioTableView tableView;
    // 내 칩 자리를 손패 위치에서 계산
    [SerializeField] private HandView handView;

    [Header("Opponents (상대)")]
    [SerializeField] private float opponentRadiusX = 5f;
    [SerializeField] private float opponentRadiusZ = 3.6f;
    [SerializeField] private float opponentOffsetZ = -1f;
    // 5인(상대 4명)일 때 자리끼리 줄 끝이 붙지 않도록 배치 타원을 키우는 배율
    [SerializeField] private float fivePlayerRadiusScale = 1.3f;
    // 5인일 때 상대 4명을 위쪽 반원에만 고르게 놓음: 오른쪽 끝 각도(도, 0 = 정오른쪽). 왼쪽 끝은 좌우 대칭(180 - 이 값)
    // 원 전체에 놓으면 아래쪽 두 명이 내 손패와 겹쳐 보여서 위로 모음
    [SerializeField] private float fivePlayerArcStart = 0f;
    // 정면(테이블 맞은편) 상대를 더 뒤로 미는 거리. 4인의 가운데 상대만 해당
    [SerializeField] private float facingSeatExtraBack = 0.8f;
    // 옆쪽(좌우) 상대를 더 뒤로 미는 거리. 옆 상대 패가 화면에서 내 손패 뒷줄과 겹치지 않게 함 (정옆일수록 많이, 정면은 0)
    [SerializeField] private float sideSeatExtraBack = 0.5f;
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
    [SerializeField] private float labelBackDistance = 3.8f;
    // 옆쪽(좌우) 상대의 이름표 거리. 옆 자리는 앞뒤로 공간이 넉넉해 패 쪽으로 더 당김 (앞뒤 방향에 가까울수록 labelBackDistance)
    [SerializeField] private float sideLabelBackDistance = 3f;
    // 5인일 때 내 좌우(양끝) 상대 이름표를 화면에서 내리려고 낮추는 높이 (정옆일수록 많이)
    [SerializeField] private float fivePlayerSideLabelDrop = 1f;

    [Header("Chips (정산 칩이 출발/도착하는 자리)")]
    // 손패/상대 패 줄에서 테이블 가운데 쪽으로 떨어진 거리
    [SerializeField] private float chipFrontGap = 0.8f;

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

    private readonly List<GameObject> backPool = new List<GameObject>();
    private readonly int[] shownTileCounts = new int[GameRules.MaxPlayers];
    private readonly Vector3[] seatAnchors = new Vector3[GameRules.MaxPlayers];
    private readonly bool[] hasSeatAnchor = new bool[GameRules.MaxPlayers];
    private readonly int[] seatBackStarts = new int[GameRules.MaxPlayers];
    private readonly Vector3[] chipAnchors = new Vector3[GameRules.MaxPlayers];
    private readonly bool[] hasChipAnchor = new bool[GameRules.MaxPlayers];
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
    private int sortMaxNumber;
    private int activeBackCount;
    private int shownMySeat = -1;
    private int shownPlayerCount;

    private void Awake()
    {
        revealComparison = CompareRevealTile;
        for (int seat = 0; seat < revealedHands.Length; seat++)
        {
            revealedHands[seat] = new List<Tile>();
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

    // ---------------- 상대 뒷면 타일 ----------------

    public void RefreshOpponents(LexioTable table)
    {
        LexioPlayer local = tableView.NetworkManager.LocalPlayer;
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
            ResetSeats();
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

    // 패 돌리기 시작: 지난 라운드 공개 패를 치우고 모든 상대를 tilesPerPlayer장으로 배치
    // 자리/인원은 동기화 값이 아직 안 왔을 수 있어서 서버가 손패와 함께 보낸 값을 사용
    public void PrepareDeal(int mySeat, int playerCount, int tilesPerPlayer)
    {
        ClearReveal();
        shownMySeat = mySeat;
        shownPlayerCount = playerCount;
        ResetSeats();
        for (int seat = 0; seat < playerCount; seat++)
        {
            shownTileCounts[seat] = tilesPerPlayer;
        }
        activeBackCount = 0;
        BuildOpponents(mySeat, playerCount);
        HideUnusedBackTiles();
    }

    // 상대 seat의 index번째 뒷면 타일 (패 돌리기 연출의 도착 위치)
    public bool TryGetBackTile(int seat, int index, out Transform backTile)
    {
        bool result = false;
        backTile = null;
        if (seat >= 0 && seat < shownTileCounts.Length && index >= 0 && index < shownTileCounts[seat])
        {
            int backIndex = seatBackStarts[seat] + index;
            if (backIndex < activeBackCount)
            {
                backTile = backPool[backIndex].transform;
                result = true;
            }
        }
        return result;
    }

    // 상대 자리 뒷면 타일 줄의 가운데쯤에서 count장 중 index번째 위치
    public bool TryGetBackTilePosition(int seat, int index, int count, out Vector3 position)
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

    // 접속 종료: 오브젝트는 풀에 남겨두고 숨기기만 함
    public void ClearAll()
    {
        ClearReveal();
        activeBackCount = 0;
        HideUnusedBackTiles();
        shownMySeat = -1;
        shownPlayerCount = 0;
        ResetSeats();
    }

    private void ResetSeats()
    {
        for (int seat = 0; seat < shownTileCounts.Length; seat++)
        {
            shownTileCounts[seat] = 0;
            hasSeatAnchor[seat] = false;
            hasChipAnchor[seat] = false;
        }
    }

    private void BuildOpponents(int mySeat, int playerCount)
    {
        // 공개 타일은 자리 배치를 다시 할 때마다 새로 놓음 (뒤집는 진행 정도는 시간으로 계산하므로 이어짐)
        ReleaseRevealTiles();
        Vector3 tileSize = tableView.TileSize;
        float spacing = tileSize.x * opponentSpacingRatio;
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
                // 내 칩 자리: 손패 맨 뒷줄에서 테이블 가운데 쪽
                chipAnchors[seat] = handView.GetChipAnchor(chipFrontGap);
                hasChipAnchor[seat] = true;
            }
            else
            {
                // 내 자리(아래, -90도)에서 반시계 방향으로 차례대로 배치
                int relative = (seat - mySeat + playerCount) % playerCount;
                float angle = GetOpponentAngle(relative, playerCount) * Mathf.Deg2Rad;
                Vector3 anchor = new Vector3(Mathf.Cos(angle) * radiusX, 0f, Mathf.Sin(angle) * radiusZ + opponentOffsetZ + sideSeatExtraBack * Mathf.Abs(Mathf.Cos(angle)));
                if (relative * 2 == playerCount)
                {
                    anchor.z += facingSeatExtraBack;
                }
                Vector3 toCenter = new Vector3(-anchor.x, 0f, opponentOffsetZ - anchor.z).normalized;
                float sideness = toCenter.x * toCenter.x;
                float labelDistance = Mathf.Lerp(labelBackDistance, sideLabelBackDistance, sideness);
                float seatLabelHeight = labelHeight;
                if (playerCount >= GameRules.MaxPlayers)
                {
                    seatLabelHeight -= fivePlayerSideLabelDrop * sideness;
                }
                seatAnchors[seat] = anchor - toCenter * labelDistance + Vector3.up * seatLabelHeight;
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

    // 내 자리(아래, -90도)에서 반시계 방향으로 몇 번째 상대인지에 따른 자리 각도(도)
    // 5인은 위쪽 반원(fivePlayerArcStart ~ 180 - fivePlayerArcStart)에 고르게, 나머지는 원 전체에 고르게
    private float GetOpponentAngle(int relative, int playerCount)
    {
        float angle = -90f + 360f * relative / playerCount;
        if (playerCount >= GameRules.MaxPlayers)
        {
            float arc = 180f - fivePlayerArcStart * 2f;
            angle = fivePlayerArcStart + arc * (relative - 1) / (playerCount - 2);
        }
        return angle;
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

    private GameObject AcquireBackTile()
    {
        GameObject back = null;
        if (activeBackCount < backPool.Count)
        {
            back = backPool[activeBackCount];
        }
        else
        {
            back = tableView.CreateBackTile();
            if (back != null)
            {
                backPool.Add(back);
            }
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

    // ---------------- 라운드 끝 남은 패 공개 ----------------

    public void OnHandsRevealed(byte[] tileIds, int[] counts)
    {
        ClearReveal();
        if (tileIds != null && counts != null)
        {
            sortMaxNumber = tableView.GetMaxNumber();
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

    public void UpdateReveal()
    {
        if (isRevealPending && Time.time >= revealApplyTime)
        {
            // 다음 상대 배치 갱신 때 뒷면 타일 대신 공개 타일을 놓음
            isRevealPending = false;
            isRevealed = true;
            isRevealDirty = true;
            isRevealFlipping = true;
            revealStartTime = Time.time;
            tableView.RequestRefresh();
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

    // 상대 한 명의 남은 패를 그 자리 줄에 앞면이 위로 오게 눕힘. 타일 위쪽은 그 상대 쪽(바깥)을 향함
    private void PlaceRevealedHand(int seat, Vector3 anchor, Vector3 toCenter, Vector3 rowDirection)
    {
        List<Tile> hand = revealedHands[seat];
        Vector3 tileSize = tableView.TileSize;
        float spacing = tileSize.x * revealSpacingRatio;
        // 눕혀 놓은 타일이라 줄 사이는 타일 세로 길이만큼 띄움
        float rowDepth = tileSize.y + opponentRowGap;
        Quaternion endRotation = Quaternion.LookRotation(Vector3.down, -toCenter);
        float elapsed = Time.time - revealStartTime;
        for (int i = 0; i < hand.Count; i++)
        {
            RevealTile reveal = new RevealTile();
            reveal.view = tableView.AcquireTile(revealPool, hand[i], false);
            reveal.position = anchor + GetOpponentRowOffset(i, hand.Count, spacing, rowDepth, toCenter, rowDirection) + Vector3.up * revealHeight;
            reveal.endRotation = endRotation;
            reveal.startTime = revealTiles.Count * revealStagger;
            revealTiles.Add(reveal);
            ApplyRevealTile(reveal, elapsed);
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
            tableView.ReleaseTile(revealTiles[i].view);
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
}
