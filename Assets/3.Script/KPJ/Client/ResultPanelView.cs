using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 게임 결과 창: 순위대로 줄마다 순위 배지 / 프로필 / 이름(+나 표시) / 칩 증감 / 최종 칩
// 순위 계산은 LexioUI가 하고, 여기는 받은 순서대로 그리기만 함
public class ResultPanelView : MonoBehaviour
{
    // 결과 한 줄의 UI 칸
    [System.Serializable]
    public class ResultRow
    {
        public GameObject root;
        public Image background;
        public Image rankBadge;
        public Image profileImage;
        public Text nameText;
        // "나" 표시. 이름 글자 바로 오른쪽에 붙임
        public RectTransform meTag;
        public Text changeText;
        public Text chipText;
    }

    [Header("Rows (위에서부터 순서대로)")]
    [SerializeField] private ResultRow[] rows = new ResultRow[0];
    // 1위 줄 배경 / 나머지 줄 배경
    [SerializeField] private Sprite firstRowSprite;
    [SerializeField] private Sprite defaultRowSprite;
    // 인덱스 = 순위 - 1 (1위 금색 ~ 5위 회색, 숫자 포함 이미지)
    [SerializeField] private Sprite[] rankBadgeSprites = new Sprite[0];
    // 내 줄을 둘러싸는 테두리. 내 줄 크기보다 meHighlightPadding만큼 크게 맞춤
    [SerializeField] private RectTransform meHighlight;
    [SerializeField] private Vector2 meHighlightPadding = new Vector2(18f, 16f);
    // 이름과 "나" 표시 사이 간격
    [SerializeField] private float meTagGap = 12f;

    [Header("Texts")]
    // "5라운드 종료 · 최종 칩 순위"
    [SerializeField] private Text subtitleText;
    [SerializeField] private Color chipGainColor = new Color(0.2f, 0.6f, 0.25f, 1f);
    [SerializeField] private Color chipLossColor = new Color(0.85f, 0.25f, 0.2f, 1f);
    [SerializeField] private Color chipSameColor = new Color(0.45f, 0.4f, 0.5f, 1f);

    [Header("Light Rays (제목 뒤에서 천천히 도는 빛)")]
    [SerializeField] private RectTransform lightRays;
    // 초당 회전 각도
    [SerializeField] private float lightRaysSpeed = 6f;

    private readonly NicknameFitter nicknameFitter = new NicknameFitter();

    private void Update()
    {
        if (lightRays != null)
        {
            lightRays.Rotate(0f, 0f, -lightRaysSpeed * Time.deltaTime);
        }
    }

    // order: 위에서부터 표시할 자리 번호, ranks/chips: 자리별 순위/최종 칩
    public void Show(LexioTable table, int mySeat, List<int> order, int[] ranks, int[] chips, Sprite[] profileSprites)
    {
        if (subtitleText != null)
        {
            subtitleText.text = table.RoundCount + "라운드 종료 · 최종 칩 순위";
        }

        bool hasMyRow = false;
        for (int i = 0; i < rows.Length; i++)
        {
            ResultRow row = rows[i];
            bool isUsed = i < order.Count;
            if (row.root != null && row.root.activeSelf != isUsed)
            {
                row.root.SetActive(isUsed);
            }
            if (isUsed)
            {
                int seat = order[i];
                bool isMine = seat == mySeat;
                FillRow(row, table.Seats[seat], ranks[seat], chips[seat], table.StartingChips, isMine, profileSprites);
                if (isMine)
                {
                    hasMyRow = true;
                    PlaceMeHighlight(row);
                }
            }
        }

        if (meHighlight != null && meHighlight.gameObject.activeSelf != hasMyRow)
        {
            meHighlight.gameObject.SetActive(hasMyRow);
        }
    }

    private void FillRow(ResultRow row, SeatInfo info, int rank, int chips, int startingChips, bool isMine, Sprite[] profileSprites)
    {
        if (row.background != null)
        {
            Sprite rowSprite = defaultRowSprite;
            if (rank == 1 && firstRowSprite != null)
            {
                rowSprite = firstRowSprite;
            }
            if (rowSprite != null)
            {
                row.background.sprite = rowSprite;
            }
        }

        Sprite badge = GetSprite(rankBadgeSprites, rank - 1);
        if (row.rankBadge != null && badge != null)
        {
            row.rankBadge.sprite = badge;
        }

        Sprite profile = GetSprite(profileSprites, info.profileImage);
        if (profile == null)
        {
            profile = GetSprite(profileSprites, 0);
        }
        if (row.profileImage != null && profile != null)
        {
            row.profileImage.sprite = profile;
        }

        if (row.nameText != null)
        {
            string suffix = "";
            if (info.isAi)
            {
                suffix = " (AI)";
            }
            nicknameFitter.SetText(row.nameText, info.nickname, suffix, 0f);
        }

        if (row.meTag != null)
        {
            if (row.meTag.gameObject.activeSelf != isMine)
            {
                row.meTag.gameObject.SetActive(isMine);
            }
            if (isMine && row.nameText != null)
            {
                // 이름 글자 끝 바로 오른쪽 (이름 칸보다 길면 칸 끝)
                RectTransform nameRect = row.nameText.rectTransform;
                float nameWidth = Mathf.Min(row.nameText.preferredWidth, nameRect.rect.width);
                Vector2 position = row.meTag.anchoredPosition;
                position.x = nameRect.anchoredPosition.x + nameWidth + meTagGap;
                row.meTag.anchoredPosition = position;
            }
        }

        if (row.changeText != null)
        {
            int change = chips - startingChips;
            if (change > 0)
            {
                row.changeText.text = "+" + change;
                row.changeText.color = chipGainColor;
            }
            else if (change < 0)
            {
                row.changeText.text = change.ToString();
                row.changeText.color = chipLossColor;
            }
            else
            {
                row.changeText.text = "±0";
                row.changeText.color = chipSameColor;
            }
        }

        if (row.chipText != null)
        {
            row.chipText.text = chips.ToString();
        }
    }

    // 내 줄과 같은 위치에, 패딩만큼 크게
    private void PlaceMeHighlight(ResultRow row)
    {
        if (meHighlight != null && row.root != null)
        {
            RectTransform rowRect = row.root.GetComponent<RectTransform>();
            meHighlight.anchoredPosition = rowRect.anchoredPosition;
            meHighlight.sizeDelta = rowRect.sizeDelta + meHighlightPadding;
        }
    }

    private Sprite GetSprite(Sprite[] sprites, int index)
    {
        Sprite result = null;
        if (sprites != null && index >= 0 && index < sprites.Length)
        {
            result = sprites[index];
        }
        return result;
    }
}
