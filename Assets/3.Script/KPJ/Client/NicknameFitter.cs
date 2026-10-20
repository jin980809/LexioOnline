using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 닉네임을 글자 칸 안에 맞춰 넣음
// 닉네임 앞 MaxShownLength(8)자까지는 글자 크기를 줄여서라도 칸 안에 다 보이게 하고, 그보다 길면 그 크기 그대로 뒤를 잘라 "..."를 붙임
// 글자 크기는 넣을 때마다 원래 크기부터 다시 계산하므로, 긴 이름 다음에 짧은 이름이 오면 원래 크기로 돌아감
// LexioUI(이름표, 내 정보, 차례 배너, 낸 패 문구)와 ResultPanelView(결과 창)가 같은 규칙을 쓰도록 한곳에 둠
public class NicknameFitter
{
    public const int MaxShownLength = 8;
    private const string Ellipsis = "...";

    // 글자 칸마다 처음 본 글자 크기 (씬에 설정된 원래 크기)
    private readonly Dictionary<Text, int> baseFontSizes = new Dictionary<Text, int>();

    // text 칸에 닉네임 + suffix를 넣음. 줄이는 것은 글자 크기와 닉네임 부분만 (suffix는 그대로 다 보임)
    // maxWidth가 0 이하면 글자 칸(RectTransform) 너비를 씀
    public void SetText(Text text, string nickname, string suffix, float maxWidth)
    {
        if (text != null)
        {
            float width = maxWidth;
            if (width <= 0f)
            {
                width = text.rectTransform.rect.width;
            }

            string name = nickname;
            if (name == null)
            {
                name = "";
            }
            bool isCut = name.Length > MaxShownLength;
            string head = Cut(name, MaxShownLength);

            // 앞 8자(이하)가 다 들어가는 크기로 맞춤
            text.fontSize = GetBaseFontSize(text);
            text.text = head + suffix;
            FitFontSize(text, width);

            // 더 길면 그 크기 그대로 "..."까지 들어가도록 닉네임 뒤를 줄임
            if (isCut)
            {
                int length = head.Length;
                text.text = head + Ellipsis + suffix;
                while (text.preferredWidth > width && length > 1)
                {
                    length--;
                    text.text = Cut(head, length) + Ellipsis + suffix;
                }
            }
        }
    }

    // 여러 줄 글자(대기실 목록 등)용: 8자보다 길면 8자 + "..." (크기 조절 없음)
    public string Shorten(string nickname)
    {
        string result = nickname;
        if (nickname != null && nickname.Length > MaxShownLength)
        {
            result = Cut(nickname, MaxShownLength) + Ellipsis;
        }
        return result;
    }

    private int GetBaseFontSize(Text text)
    {
        int size;
        if (baseFontSizes.TryGetValue(text, out size) == false)
        {
            size = text.fontSize;
            baseFontSizes.Add(text, size);
        }
        return size;
    }

    // 넘치면 너비 비율만큼 한 번에 줄이고, 그래도 넘치면 1씩 줄임
    private void FitFontSize(Text text, float width)
    {
        float preferred = text.preferredWidth;
        if (width > 0f && preferred > width)
        {
            int size = Mathf.Max(1, Mathf.FloorToInt(text.fontSize * width / preferred));
            text.fontSize = size;
            while (text.preferredWidth > width && size > 1)
            {
                size--;
                text.fontSize = size;
            }
        }
    }

    // 앞에서 length자 (이모지처럼 두 칸을 쓰는 글자가 반으로 잘리지 않게 함)
    private string Cut(string value, int length)
    {
        int end = Mathf.Min(length, value.Length);
        if (end > 0 && end < value.Length && char.IsHighSurrogate(value[end - 1]))
        {
            end--;
        }
        return value.Substring(0, end);
    }
}
