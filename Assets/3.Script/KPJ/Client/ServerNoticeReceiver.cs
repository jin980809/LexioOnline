using System;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

// 서버 공지(ServerNoticeMessage, 서버 콘솔의 notice 명령)를 메인 화면 공지란에 표시. 빈 공지가 오면 원래 문구로 되돌림
// 클라이언트가 접속을 끊으면 Mirror가 메시지 등록을 지우므로, 클라이언트가 켜질 때마다 다시 등록함 (로그인은 메인 화면에서 함)
// 게임 중이라 공지란이 꺼져 있으면 문구만 바꿔 두고, 메인으로 돌아와 켜질 때 NoticeUI가 처음부터 흘림
// 인게임 공지 띠(InGameNoticeBanner)는 게임 중에 NoticeChanged로 받음
public class ServerNoticeReceiver : MonoBehaviour
{
    [SerializeField] private NoticeUI noticeUI;
    [SerializeField] private Text noticeText;

    private string defaultNotice = "";
    private string currentNotice = "";
    private bool isRegistered;

    // 서버 공지가 바뀔 때. 빈 문자열이면 공지가 지워진 것
    public event Action<string> NoticeChanged;

    private void Start()
    {
        if (noticeText != null)
        {
            defaultNotice = noticeText.text;
        }
    }

    private void Update()
    {
        if (NetworkClient.active)
        {
            if (isRegistered == false)
            {
                NetworkClient.ReplaceHandler<ServerNoticeMessage>(OnNotice, false);
                isRegistered = true;
            }
        }
        else if (isRegistered)
        {
            // 접속이 끊기면 그 서버의 공지는 지움 (다른 서버에 다시 접속했을 때 예전 공지가 인게임에 뜨지 않게)
            isRegistered = false;
            if (currentNotice.Length > 0)
            {
                OnNotice(new ServerNoticeMessage { text = "" });
            }
        }
    }

    private void OnNotice(ServerNoticeMessage message)
    {
        currentNotice = message.text;
        if (currentNotice == null)
        {
            currentNotice = "";
        }

        string text = currentNotice;
        if (text.Length == 0)
        {
            text = defaultNotice;
        }

        if (noticeUI != null && noticeUI.isActiveAndEnabled)
        {
            noticeUI.SetNotice(text);
        }
        else if (noticeText != null)
        {
            noticeText.text = text;
        }

        if (NoticeChanged != null)
        {
            NoticeChanged(currentNotice);
        }
    }
}
