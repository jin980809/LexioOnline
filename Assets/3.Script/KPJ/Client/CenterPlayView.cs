using System.Collections.Generic;
using UnityEngine;

// 가운데 마지막 낸 패 표시 + 낸 패가 낸 자리에서 가운데로 날아가는 연출
public class CenterPlayView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LexioTableView tableView;
    // 내가 낸 패의 출발 위치 (아직 손패에 보이는 타일)
    [SerializeField] private HandView handView;
    // 상대가 낸 패의 출발 위치 (그 상대의 뒷면 타일 줄)
    [SerializeField] private OpponentView opponentView;

    [Header("Center (마지막 낸 패)")]
    [SerializeField] private Vector3 centerPosition = new Vector3(0f, 0.25f, -0.5f);
    [SerializeField] private Vector3 centerTileEuler = new Vector3(90f, 0f, 0f);

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

    private readonly List<TileView> centerViews = new List<TileView>();
    private readonly Dictionary<byte, TileView> centerPool = new Dictionary<byte, TileView>();
    private readonly List<byte> shownCenterIds = new List<byte>();
    private readonly List<PlayFlight> playFlights = new List<PlayFlight>();
    private readonly Dictionary<byte, PlayOrigin> playOrigins = new Dictionary<byte, PlayOrigin>();
    private float playStartTime;

    // ---------------- 가운데 마지막 패 ----------------

    public void RefreshCenter(LexioTable table)
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
                bool canAnimate = table.Phase == GamePhase.Playing && tableView.IsDealing == false;
                float spacing = tableView.GetTileWidth() + tableView.TileGap;
                float startX = -(tileIds.Count - 1) * spacing * 0.5f;
                Quaternion rotation = Quaternion.Euler(centerTileEuler);
                for (int i = 0; i < tileIds.Count; i++)
                {
                    TileView view = tableView.AcquireTile(centerPool, new Tile(tileIds[i]), false);
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

    // 손패에서 빠지는 타일 위치를 기억 (가운데 패가 늦게 오면 여기서 출발)
    public void RememberPlayOrigin(TileView view)
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

    // 새 라운드: 지난 라운드에 기억한 출발 위치는 버림
    public void ClearPlayOrigins()
    {
        playOrigins.Clear();
    }

    // 접속 종료: 오브젝트는 풀에 남겨두고 숨기기만 함
    public void ClearAll()
    {
        ReleaseCenter();
        playOrigins.Clear();
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
            tableView.ReleaseTile(centerViews[i]);
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
        if (handView.TryGetShownTile(tileId, out TileView handTile))
        {
            // 가운데 패가 먼저 온 경우: 아직 손패에 있는 타일을 숨기고 그 자리에서 출발 (손패에서는 곧 RPC로 빠짐)
            flight.startPosition = handTile.transform.position;
            flight.startRotation = handTile.transform.rotation;
            handView.ReleaseFromInput(handTile);
            handTile.gameObject.SetActive(false);
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
        else if (opponentView.TryGetBackTilePosition(playSeat, index, count, out Vector3 backPosition))
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

    public void UpdatePlay()
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
}
