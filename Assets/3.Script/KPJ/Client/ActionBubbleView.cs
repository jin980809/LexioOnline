using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 상대가 패를 내거나 패스하면 그 사람 이름표 위에 말풍선을 잠깐 띄움 (내 행동은 표시 안 함)
// 서버가 행동마다 RPC로 알려주고(LexioTable.RpcShowPlay / RpcShowPass), 문구는 여기서 만듦
// 라운드 정산 중에는 LexioUI가 넘겨준 정산 계산식을 모든 자리 말풍선에 띄움 (내 것은 내 정보 말풍선)
public class ActionBubbleView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LexioTableNetworkManager networkManager;
    // 자리 번호 순서. 각 상대 이름표의 자식이라 이름표를 따라 움직이고, 내 자리 이름표는 꺼져 있음
    [SerializeField] private GameObject[] seatBubbles;
    [SerializeField] private Text[] seatBubbleTexts;
    // 내 정보 패널의 말풍선. 정산 계산식만 표시
    [SerializeField] private GameObject myBubble;
    [SerializeField] private Text myBubbleText;

    [Header("Bubble")]
    // 말풍선이 떠 있는 시간. 같은 사람이 다시 행동하면 새 내용으로 바뀌고 시간도 다시 셈
    [SerializeField] private float bubbleSeconds = 2f;

    private readonly List<Tile> playTiles = new List<Tile>();
    // 문구 형식("족보 문양 숫자")은 가운데 패 문구와 같이 씀
    private readonly CombinationText combinationText = new CombinationText();
    private float[] hideTimes = new float[0];
    // 자리별 정산 계산식. 비어 있으면 정산 중이 아님
    private string[] settleTexts = new string[0];

    private void OnEnable()
    {
        networkManager.PlayReceived += OnPlayReceived;
        networkManager.PassReceived += OnPassReceived;
    }

    private void OnDisable()
    {
        networkManager.PlayReceived -= OnPlayReceived;
        networkManager.PassReceived -= OnPassReceived;
    }

    private void Awake()
    {
        hideTimes = new float[seatBubbles.Length];
        settleTexts = new string[seatBubbles.Length];
        for (int i = 0; i < seatBubbles.Length; i++)
        {
            settleTexts[i] = "";
            SetActive(seatBubbles[i], false);
        }
        if (myBubble != null)
        {
            SetActive(myBubble, false);
        }
    }

    private void Update()
    {
        LexioTable table = networkManager.Table;
        bool isPlaying = table != null && table.Phase == GamePhase.Playing;
        bool canShowSettle = table != null && table.Phase != GamePhase.Waiting;
        int mySeat = GetMySeat();
        bool isMyBubbleVisible = false;
        for (int seat = 0; seat < seatBubbles.Length; seat++)
        {
            bool hasSettle = canShowSettle && settleTexts[seat].Length > 0;
            bool isVisible = false;
            if (seat == mySeat)
            {
                isMyBubbleVisible = hasSettle;
            }
            else
            {
                isVisible = hasSettle || (isPlaying && Time.time < hideTimes[seat]);
            }
            SetActive(seatBubbles[seat], isVisible);
        }

        if (myBubble != null)
        {
            SetActive(myBubble, isMyBubbleVisible);
        }
    }

    // 정산 계산식 표시. 빈 문자열이면 그 자리 정산 표시를 끔
    public void SetSettleText(int seat, string text)
    {
        if (seat >= 0 && seat < settleTexts.Length)
        {
            settleTexts[seat] = text;
            if (text.Length > 0)
            {
                // 정산 직전의 행동 말풍선이 정산이 끝난 뒤 다시 뜨지 않도록 함
                hideTimes[seat] = 0f;
                if (seat == GetMySeat())
                {
                    if (myBubbleText != null)
                    {
                        myBubbleText.text = text;
                    }
                }
                else
                {
                    seatBubbleTexts[seat].text = text;
                }
            }
        }
    }

    public void ClearSettleTexts()
    {
        for (int i = 0; i < settleTexts.Length; i++)
        {
            settleTexts[i] = "";
        }
    }

    private void OnPassReceived(int seat)
    {
        ShowBubble(seat, "패스");
    }

    private void OnPlayReceived(int seat, byte[] tileIds)
    {
        LexioTable table = networkManager.Table;
        if (table != null && tileIds != null)
        {
            playTiles.Clear();
            for (int i = 0; i < tileIds.Length; i++)
            {
                playTiles.Add(new Tile(tileIds[i]));
            }

            Combination combination = CombinationEvaluator.Evaluate(playTiles, table.MaxNumber);
            if (combination != null)
            {
                ShowBubble(seat, combinationText.GetPlayText(combination, table.MaxNumber));
            }
        }
    }

    // 내 행동(낸 패/패스)은 내가 이미 알고 있으므로 띄우지 않음
    private void ShowBubble(int seat, string message)
    {
        bool isMine = GetMySeat() == seat;
        if (seat >= 0 && seat < seatBubbles.Length && isMine == false)
        {
            seatBubbleTexts[seat].text = message;
            hideTimes[seat] = Time.time + bubbleSeconds;
        }
    }

    private int GetMySeat()
    {
        int seat = -1;
        LexioPlayer local = networkManager.LocalPlayer;
        if (local != null)
        {
            seat = local.Seat;
        }
        return seat;
    }

    private void SetActive(GameObject target, bool isActive)
    {
        if (target.activeSelf != isActive)
        {
            target.SetActive(isActive);
        }
    }
}
