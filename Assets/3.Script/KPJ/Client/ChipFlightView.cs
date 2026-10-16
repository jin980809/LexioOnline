using System.Collections.Generic;
using UnityEngine;

// 라운드 정산 때 잃은 사람 자리에서 딴 사람 자리로 칩을 1개씩 날려 보냄 (쌓아 두지 않고 날아가는 칩만 보임)
// 실제 규칙대로 모든 두 사람끼리 주고받는 과정을 꼴등(정산 기준 장수가 가장 많은 사람)부터 순서대로 보여줌
// 서버가 칩과 함께 보낸 정산 기준 장수(SeatInfo.settleCount)로 계산하고,
// 그 값이 칩 변화와 안 맞으면(동기화 누락 등) 칩 차이만큼 잃은 사람 → 딴 사람으로 나눠서 보냄
// UI 칩 숫자는 GetShownChips로 읽음: 칩이 출발할 때 보낸 사람 -1, 도착할 때 받은 사람 +1 이라 숫자가 1씩 순서대로 바뀜
// 칩 오브젝트는 풀링해서 재사용
public class ChipFlightView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LexioTableNetworkManager networkManager;
    [SerializeField] private LexioTableView clientView;
    [SerializeField] private GameObject chipPrefab;

    [Header("Chip")]
    // 칩 1개의 지름. 프리팹 크기를 재서 이 크기에 맞게 배율을 정함
    [SerializeField] private float chipDiameter = 0.4f;

    [Header("Settle (정산 칩 날리기)")]
    [SerializeField] private float chipFlightSeconds = 0.4f;
    // 같은 두 사람 사이에서 칩 1개씩 출발하는 간격
    [SerializeField] private float chipStagger = 0.08f;
    // 한 사람에게 다 주고 다음 사람에게 넘어갈 때 쉬는 시간
    [SerializeField] private float pairGap = 0.2f;
    // 칩이 많아도 이 시간 안에 다 도착하도록 간격을 줄임 (서버 LexioTable.roundEndDelaySeconds보다 짧아야 다음 라운드 전에 끝남)
    [SerializeField] private float maxSettleSeconds = 4f;
    [SerializeField] private float chipArcHeight = 0.8f;
    // 날아가는 동안 앞뒤로 뒤집히는 바퀴 수
    [SerializeField] private int chipSpinTurns = 1;

    // 보낸 사람 자리에서 받은 사람 자리로 날아가는 칩 1개
    private struct ChipFlight
    {
        public int fromSeat;
        public int toSeat;
        public GameObject chip;
        public Vector3 startPosition;
        public Vector3 endPosition;
        // 전체 칩 중 몇 번째인지, 몇 번째 주고받기(두 사람 짝)인지 → 출발 시간 계산용
        public int chipOrder;
        public int pairOrder;
        public float startTime;
        public bool isStarted;
        public bool isLanded;
    }

    // 화면(UI)에 보이는 칩 수. 정산 중에는 칩이 1개씩 오가며 바뀌고, 끝나면 table.Seats[].chips와 같아짐
    private readonly int[] shownChips = new int[GameRules.MaxPlayers];
    private readonly int[] remainingChanges = new int[GameRules.MaxPlayers];
    // 정산 기준 장수가 많은 순(꼴등 → 1등)으로 정렬한 자리 번호
    private readonly int[] settleOrder = new int[GameRules.MaxPlayers];
    private int nextChipOrder;
    private int nextPairOrder;
    private readonly List<GameObject> chipPool = new List<GameObject>();
    private readonly List<ChipFlight> chipFlights = new List<ChipFlight>();
    private int shownSeatCount;
    private int cachedVersion = -1;
    private bool isRefreshNeeded;
    private float settleStartTime;
    private bool isChipMeasured;
    private float chipScale = 1f;
    private float chipHeight;
    private Vector3 chipCenterOffset;

    // 보이는 칩 수가 바뀔 때마다 증가 (UI가 이 값이 바뀌면 칩 숫자를 다시 그림)
    public int ChipVersion { get; private set; }

    private void Awake()
    {
        if (chipPrefab == null)
        {
            Debug.LogWarning("[Lexio] ChipFlightView에 chipPrefab이 없어 정산 칩이 날아가지 않습니다. Lexio/Setup Project를 다시 실행하세요.");
        }
    }

    private void Update()
    {
        UpdateFlights();

        LexioTable table = networkManager.Table;
        int version = -1;
        if (table != null)
        {
            version = table.Version;
        }

        if (version != cachedVersion || isRefreshNeeded)
        {
            cachedVersion = version;
            isRefreshNeeded = false;
            Refresh(table);
        }
    }

    // UI에 보여줄 칩 수. 아직 받은 칩 정보가 없는 자리는 동기화된 값을 그대로 씀
    public int GetShownChips(int seat, int syncedChips)
    {
        int result = syncedChips;
        if (seat >= 0 && seat < shownSeatCount)
        {
            result = shownChips[seat];
        }
        return result;
    }

    private void Refresh(LexioTable table)
    {
        int seatCount = 0;
        if (table != null && table.Phase != GamePhase.Waiting)
        {
            seatCount = table.Seats.Count;
        }

        if (seatCount != shownSeatCount)
        {
            // 게임 시작/종료, 접속/종료: 연출 없이 바로 맞춤
            Snap(table, seatCount);
        }
        else if (seatCount > 0 && chipFlights.Count == 0 && HasChipChange(table))
        {
            // 날아가는 중에 바뀐 값은 연출이 끝난 뒤 다시 확인 (isRefreshNeeded)
            if (table.Phase == GamePhase.Playing)
            {
                StartSettle(table);
            }
            else
            {
                Snap(table, seatCount);
            }
        }
    }

    private bool HasChipChange(LexioTable table)
    {
        bool result = false;
        for (int seat = 0; seat < shownSeatCount; seat++)
        {
            if (table.Seats[seat].chips != shownChips[seat])
            {
                result = true;
            }
        }
        return result;
    }

    private void Snap(LexioTable table, int seatCount)
    {
        StopFlights();
        for (int seat = 0; seat < shownChips.Length; seat++)
        {
            shownChips[seat] = 0;
            if (seat < seatCount)
            {
                shownChips[seat] = table.Seats[seat].chips;
            }
        }
        shownSeatCount = seatCount;
        ChipVersion++;
    }

    // ---------------- 정산 연출 ----------------

    // 주고받기를 한 번에 한 짝씩 순서대로 보여줌 (같은 짝 안에서는 chipStagger 간격으로 1개씩)
    private void StartSettle(LexioTable table)
    {
        if (AddRuleFlights(table) == false)
        {
            chipFlights.Clear();
            AddDifferenceFlights(table);
        }

        // 주고받는 양이 안 맞는 몫(규칙상 없음)은 연출 없이 바로 반영
        for (int seat = 0; seat < shownSeatCount; seat++)
        {
            if (remainingChanges[seat] != 0)
            {
                shownChips[seat] += remainingChanges[seat];
                ChipVersion++;
            }
        }

        ScheduleFlights();
        settleStartTime = Time.time;
    }

    // 실제 규칙: 모든 두 사람끼리 정산 기준 장수가 많은 쪽이 차이만큼 적은 쪽에게 줌
    // 꼴등(장수가 가장 많은 사람)부터 주는 사람이 되고, 받는 사람은 1등부터 차례로
    // 계산 결과가 실제 칩 변화와 같을 때만 true
    private bool AddRuleFlights(LexioTable table)
    {
        ResetRemainingChanges(table);
        nextChipOrder = 0;
        nextPairOrder = 0;

        // 장수 많은 순 정렬 (같으면 자리 번호 순). 인원이 5명 이하라 삽입 정렬
        for (int i = 0; i < shownSeatCount; i++)
        {
            int seat = i;
            int position = i;
            while (position > 0 && IsSettleBefore(table, seat, settleOrder[position - 1]))
            {
                settleOrder[position] = settleOrder[position - 1];
                position--;
            }
            settleOrder[position] = seat;
        }

        for (int payerIndex = 0; payerIndex < shownSeatCount; payerIndex++)
        {
            int payer = settleOrder[payerIndex];
            for (int receiverIndex = shownSeatCount - 1; receiverIndex > payerIndex; receiverIndex--)
            {
                int receiver = settleOrder[receiverIndex];
                int amount = table.Seats[payer].settleCount - table.Seats[receiver].settleCount;
                if (amount > 0)
                {
                    AddPairFlights(payer, receiver, amount);
                }
            }
        }

        bool result = true;
        for (int seat = 0; seat < shownSeatCount; seat++)
        {
            if (remainingChanges[seat] != 0)
            {
                result = false;
            }
        }
        return result;
    }

    private bool IsSettleBefore(LexioTable table, int seat, int otherSeat)
    {
        int count = table.Seats[seat].settleCount;
        int otherCount = table.Seats[otherSeat].settleCount;
        return count > otherCount || (count == otherCount && seat < otherSeat);
    }

    // 정산 기준 장수를 못 받은 경우: 칩 차이만큼 잃은 사람 → 딴 사람(자리 순서)으로 나눠 보냄
    private void AddDifferenceFlights(LexioTable table)
    {
        ResetRemainingChanges(table);
        nextChipOrder = 0;
        nextPairOrder = 0;
        for (int fromSeat = 0; fromSeat < shownSeatCount; fromSeat++)
        {
            for (int toSeat = 0; toSeat < shownSeatCount; toSeat++)
            {
                if (remainingChanges[fromSeat] < 0 && remainingChanges[toSeat] > 0)
                {
                    AddPairFlights(fromSeat, toSeat, Mathf.Min(-remainingChanges[fromSeat], remainingChanges[toSeat]));
                }
            }
        }
    }

    // 남은 변화량 = 받아야 할 칩 수 (주면 음수). 칩을 보낼 때마다 0에 가까워짐
    private void ResetRemainingChanges(LexioTable table)
    {
        for (int seat = 0; seat < remainingChanges.Length; seat++)
        {
            remainingChanges[seat] = 0;
            if (seat < shownSeatCount)
            {
                remainingChanges[seat] = table.Seats[seat].chips - shownChips[seat];
            }
        }
    }

    // 두 사람 사이 주고받기 1번 (amount개)
    private void AddPairFlights(int fromSeat, int toSeat, int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            ChipFlight flight = new ChipFlight();
            flight.fromSeat = fromSeat;
            flight.toSeat = toSeat;
            flight.chipOrder = nextChipOrder;
            flight.pairOrder = nextPairOrder;
            flight.isStarted = false;
            flight.isLanded = false;
            chipFlights.Add(flight);
            nextChipOrder++;
        }
        remainingChanges[fromSeat] += amount;
        remainingChanges[toSeat] -= amount;
        nextPairOrder++;
    }

    // 출발 시간 = 칩 순서 × chipStagger + 짝 순서 × pairGap. 전체가 maxSettleSeconds를 넘으면 같은 비율로 줄임
    private void ScheduleFlights()
    {
        if (chipFlights.Count > 0)
        {
            ChipFlight last = chipFlights[chipFlights.Count - 1];
            float rawSeconds = last.chipOrder * chipStagger + last.pairOrder * pairGap;
            float limitSeconds = Mathf.Max(0f, maxSettleSeconds - Mathf.Max(0.01f, chipFlightSeconds));
            float scale = 1f;
            if (rawSeconds > limitSeconds && rawSeconds > 0f)
            {
                scale = limitSeconds / rawSeconds;
            }

            for (int i = 0; i < chipFlights.Count; i++)
            {
                ChipFlight flight = chipFlights[i];
                flight.startTime = (flight.chipOrder * chipStagger + flight.pairOrder * pairGap) * scale;
                chipFlights[i] = flight;
            }
        }
    }

    private void UpdateFlights()
    {
        if (chipFlights.Count > 0)
        {
            float elapsed = Time.time - settleStartTime;
            float flightSeconds = Mathf.Max(0.01f, chipFlightSeconds);
            bool isAllLanded = true;
            for (int i = 0; i < chipFlights.Count; i++)
            {
                ChipFlight flight = chipFlights[i];
                if (flight.isLanded == false)
                {
                    if (flight.isStarted == false && elapsed >= flight.startTime)
                    {
                        flight = BeginFlight(flight);
                    }

                    if (flight.isStarted)
                    {
                        float t = (elapsed - flight.startTime) / flightSeconds;
                        if (t >= 1f)
                        {
                            LandFlight(flight);
                            flight.isLanded = true;
                        }
                        else
                        {
                            isAllLanded = false;
                            MoveFlight(flight, t);
                        }
                    }
                    else
                    {
                        isAllLanded = false;
                    }
                    chipFlights[i] = flight;
                }
            }

            if (isAllLanded)
            {
                chipFlights.Clear();
                // 연출 중에 칩 값이 또 바뀌었을 수 있으므로 다시 확인
                isRefreshNeeded = true;
            }
        }
    }

    // 보낸 사람 칩 숫자를 1 줄이고 그 자리에서 칩을 꺼냄 (자리 위치를 모르면 숫자만 바뀜)
    private ChipFlight BeginFlight(ChipFlight flight)
    {
        shownChips[flight.fromSeat]--;
        ChipVersion++;

        if (clientView.TryGetChipAnchor(flight.fromSeat, out Vector3 fromAnchor) && clientView.TryGetChipAnchor(flight.toSeat, out Vector3 toAnchor))
        {
            MeasureChip();
            // 칩 중심을 테이블 면 위에 맞추고 피벗 기준으로 되돌림
            Vector3 lift = Vector3.up * (chipHeight * 0.5f) - chipCenterOffset;
            flight.startPosition = fromAnchor + lift;
            flight.endPosition = toAnchor + lift;
            flight.chip = AcquireChip();
            if (flight.chip != null)
            {
                flight.chip.transform.SetPositionAndRotation(flight.startPosition, Quaternion.identity);
            }
        }
        flight.isStarted = true;
        return flight;
    }

    private void MoveFlight(ChipFlight flight, float t)
    {
        if (flight.chip != null)
        {
            float eased = 1f - (1f - t) * (1f - t);
            Vector3 position = Vector3.Lerp(flight.startPosition, flight.endPosition, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * chipArcHeight);
            Quaternion rotation = Quaternion.AngleAxis(360f * chipSpinTurns * eased, Vector3.right);
            flight.chip.transform.SetPositionAndRotation(position, rotation);
        }
    }

    // 받은 사람 칩 숫자를 1 늘리고 칩은 숨김
    private void LandFlight(ChipFlight flight)
    {
        shownChips[flight.toSeat]++;
        ChipVersion++;
        if (flight.chip != null)
        {
            ReleaseChip(flight.chip);
        }
    }

    // 날아가던 칩은 숨기고 연출 중단 (칩 숫자는 호출 측에서 다시 맞춤)
    private void StopFlights()
    {
        for (int i = 0; i < chipFlights.Count; i++)
        {
            ChipFlight flight = chipFlights[i];
            if (flight.isStarted && flight.isLanded == false && flight.chip != null)
            {
                ReleaseChip(flight.chip);
            }
        }
        chipFlights.Clear();
    }

    // ---------------- 공통 ----------------

    private GameObject AcquireChip()
    {
        GameObject chip = null;
        if (chipPool.Count > 0)
        {
            chip = chipPool[chipPool.Count - 1];
            chipPool.RemoveAt(chipPool.Count - 1);
            chip.SetActive(true);
        }
        else if (chipPrefab != null)
        {
            MeasureChip();
            chip = Instantiate(chipPrefab, transform);
            chip.transform.localScale = Vector3.one * chipScale;
        }
        return chip;
    }

    private void ReleaseChip(GameObject chip)
    {
        chip.SetActive(false);
        chipPool.Add(chip);
    }

    // 칩 배율(지름을 chipDiameter에 맞춤), 두께, 피벗→중심 오프셋을 프리팹 바운드로 한 번 측정
    private void MeasureChip()
    {
        if (isChipMeasured == false)
        {
            isChipMeasured = true;
            chipScale = 1f;
            chipHeight = chipDiameter * 0.1f;
            chipCenterOffset = Vector3.zero;
            if (chipPrefab != null)
            {
                GameObject probe = Instantiate(chipPrefab);
                probe.transform.position = Vector3.zero;
                probe.transform.rotation = Quaternion.identity;
                probe.transform.localScale = Vector3.one;
                Renderer[] probeRenderers = probe.GetComponentsInChildren<Renderer>();
                if (probeRenderers.Length > 0)
                {
                    Bounds bounds = probeRenderers[0].bounds;
                    for (int i = 1; i < probeRenderers.Length; i++)
                    {
                        bounds.Encapsulate(probeRenderers[i].bounds);
                    }
                    float width = Mathf.Max(bounds.size.x, bounds.size.z);
                    if (width > 0f)
                    {
                        chipScale = chipDiameter / width;
                        chipHeight = bounds.size.y * chipScale;
                        chipCenterOffset = bounds.center * chipScale;
                    }
                }
                DestroyImmediate(probe);
            }
        }
    }
}
