using System.Collections.Generic;
using UnityEngine;

// 라운드 시작 패 돌리기 연출: 손패와 상대 뒷면 타일을 최종 위치에 미리 배치한 뒤 숨기고, 가운데에서 한 장씩 날려 보냄
public class DealAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LexioTableView tableView;
    [SerializeField] private HandView handView;
    [SerializeField] private OpponentView opponentView;

    [Header("Deal (라운드 시작 패 돌리기)")]
    // 서버 LexioTable.dealSeconds보다 짧아야 연출 중에 턴이 시작되지 않음
    [SerializeField] private float dealSeconds = 1.8f;
    [SerializeField] private float dealFlightSeconds = 0.35f;
    [SerializeField] private float dealArcHeight = 1.2f;
    [SerializeField] private Vector3 dealOrigin = new Vector3(0f, 0.6f, -0.5f);

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

    private readonly List<DealFlight> dealFlights = new List<DealFlight>();
    private bool isDealing;
    private float dealStartTime;

    // 패 돌리는 중에는 선택/정렬/제출 불가
    public bool IsDealing
    {
        get { return isDealing; }
    }

    // 손패는 HandView.ApplyHand로 이미 놓인 상태에서 호출
    // 자리/인원은 동기화 값(local.Seat, table.Seats)이 아직 안 왔을 수 있어서 서버가 손패와 함께 보낸 값을 사용
    public void StartDeal(int mySeat, int playerCount)
    {
        if (playerCount >= GameRules.MinPlayers && playerCount <= GameRules.MaxPlayers && mySeat >= 0 && mySeat < playerCount)
        {
            int tilesPerPlayer = GameRules.GetTilesPerPlayer(playerCount);

            // 지난 라운드에 공개했던 패를 치우고 상대 뒷면 타일을 새로 놓음
            opponentView.PrepareDeal(mySeat, playerCount, tilesPerPlayer);

            int flightCount = tilesPerPlayer * playerCount;
            float interval = 0f;
            if (flightCount > 1)
            {
                interval = Mathf.Max(0f, dealSeconds - dealFlightSeconds) / (flightCount - 1);
            }

            // 실제 렉시오처럼 자리마다 돌아가며 한 장씩
            IReadOnlyList<TileView> handTiles = handView.Tiles;
            int order = 0;
            for (int lap = 0; lap < tilesPerPlayer; lap++)
            {
                for (int seat = 0; seat < playerCount; seat++)
                {
                    Transform target = null;
                    TileView handTile = null;
                    if (seat == mySeat)
                    {
                        if (lap < handTiles.Count)
                        {
                            handTile = handTiles[lap];
                            target = handTile.transform;
                        }
                    }
                    else if (opponentView.TryGetBackTile(seat, lap, out Transform backTile))
                    {
                        target = backTile;
                    }

                    if (target != null)
                    {
                        DealFlight flight = new DealFlight();
                        flight.target = target;
                        flight.handView = handTile;
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

    public void UpdateDeal()
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
            tableView.RequestRefresh();
            handView.NotifySelectionChanged();
        }
    }

    // 연출 중단 (새 패를 받았거나 접속 종료)
    public void ResetDeal()
    {
        dealFlights.Clear();
        isDealing = false;
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
}
