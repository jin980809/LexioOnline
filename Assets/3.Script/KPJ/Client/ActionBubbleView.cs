using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 상대가 패를 내거나 패스하면 그 사람 이름표 위에 말풍선을 잠깐 띄움 (내 행동은 표시 안 함)
// 서버가 행동마다 RPC로 알려주고(LexioTable.RpcShowPlay / RpcShowPass), 문구는 여기서 만듦
public class ActionBubbleView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LexioTableNetworkManager networkManager;
    // 자리 번호 순서. 각 상대 이름표의 자식이라 이름표를 따라 움직이고, 내 자리 이름표는 꺼져 있음
    [SerializeField] private GameObject[] seatBubbles;
    [SerializeField] private Text[] seatBubbleTexts;

    [Header("Bubble")]
    // 말풍선이 떠 있는 시간. 같은 사람이 다시 행동하면 새 내용으로 바뀌고 시간도 다시 셈
    [SerializeField] private float bubbleSeconds = 2f;

    private readonly List<Tile> playTiles = new List<Tile>();
    // 문구 형식("족보 문양 숫자")은 가운데 패 문구와 같이 씀
    private readonly CombinationText combinationText = new CombinationText();
    private float[] hideTimes = new float[0];

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
        for (int i = 0; i < seatBubbles.Length; i++)
        {
            SetActive(seatBubbles[i], false);
        }
    }

    private void Update()
    {
        LexioTable table = networkManager.Table;
        bool isPlaying = table != null && table.Phase == GamePhase.Playing;
        for (int seat = 0; seat < seatBubbles.Length; seat++)
        {
            bool isVisible = isPlaying && Time.time < hideTimes[seat];
            SetActive(seatBubbles[seat], isVisible);
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
        LexioPlayer local = networkManager.LocalPlayer;
        bool isMine = local != null && local.Seat == seat;
        if (seat >= 0 && seat < seatBubbles.Length && isMine == false)
        {
            seatBubbleTexts[seat].text = message;
            hideTimes[seat] = Time.time + bubbleSeconds;
        }
    }

    private void SetActive(GameObject target, bool isActive)
    {
        if (target.activeSelf != isActive)
        {
            target.SetActive(isActive);
        }
    }
}
