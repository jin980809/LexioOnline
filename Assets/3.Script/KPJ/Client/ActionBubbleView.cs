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
                ShowBubble(seat, GetPlayText(combination, table.MaxNumber));
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

    // "족보 문양 숫자" (예: 스트레이트 해 1). 기준 타일은 서열상 가장 높은 타일
    // 스트레이트의 맨 위 타일(1-2-3-4-5면 2, ~13-1이면 1)도 서열상 가장 높은 타일과 같음
    // 풀하우스/포카드는 문양을 비교하지 않으므로 트리플/포카드 숫자만 표시
    private string GetPlayText(Combination combination, int maxNumber)
    {
        string text;
        if (combination.Type == CombinationType.FullHouse)
        {
            text = combination.DisplayName + " " + GetNumberWithCount(combination.Tiles, 3);
        }
        else if (combination.Type == CombinationType.FourCard)
        {
            text = combination.DisplayName + " " + GetNumberWithCount(combination.Tiles, 4);
        }
        else
        {
            Tile top = GetHighestTile(combination.Tiles, maxNumber);
            text = combination.DisplayName + " " + GetSuitName(top.Suit) + " " + top.Number;
        }
        return text;
    }

    private Tile GetHighestTile(IReadOnlyList<Tile> tiles, int maxNumber)
    {
        Tile highest = tiles[0];
        for (int i = 1; i < tiles.Count; i++)
        {
            if (tiles[i].GetPower(maxNumber) > highest.GetPower(maxNumber))
            {
                highest = tiles[i];
            }
        }
        return highest;
    }

    // 정확히 count장 있는 숫자 (풀하우스의 트리플, 포카드의 4장)
    private int GetNumberWithCount(IReadOnlyList<Tile> tiles, int count)
    {
        int result = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            int sameCount = 0;
            for (int j = 0; j < tiles.Count; j++)
            {
                if (tiles[j].Number == tiles[i].Number)
                {
                    sameCount++;
                }
            }

            if (sameCount == count)
            {
                result = tiles[i].Number;
            }
        }
        return result;
    }

    private string GetSuitName(TileSuit suit)
    {
        string name;
        switch (suit)
        {
            case TileSuit.Cloud:
                name = "구름";
                break;
            case TileSuit.Star:
                name = "별";
                break;
            case TileSuit.Moon:
                name = "달";
                break;
            case TileSuit.Sun:
                name = "해";
                break;
            default:
                name = "";
                break;
        }
        return name;
    }

    private void SetActive(GameObject target, bool isActive)
    {
        if (target.activeSelf != isActive)
        {
            target.SetActive(isActive);
        }
    }
}
