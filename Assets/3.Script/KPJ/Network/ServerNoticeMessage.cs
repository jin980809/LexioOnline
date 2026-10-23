using Mirror;

// 서버 → 클라이언트 공지 (서버 콘솔의 notice 명령). text가 비어 있으면 공지를 지우고 원래 문구로 되돌림
public struct ServerNoticeMessage : NetworkMessage
{
    public string text;
}
