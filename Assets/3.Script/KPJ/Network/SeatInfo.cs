using System;

public enum GamePhase
{
    Waiting = 0,
    Playing = 1,
    Finished = 2
}

// 모든 클라이언트가 보는 자리 정보 (손패 내용은 절대 넣지 않음)
// IEquatable 구현: SyncList가 값 비교할 때 박싱/리플렉션 비교를 피하기 위함
public struct SeatInfo : IEquatable<SeatInfo>
{
    public string nickname;
    public int chips;
    public int tileCount;
    public bool isAi;
    // 패 정리 시간에 준비 완료를 눌렀는지 (라운드마다 초기화)
    public bool isReady;
    // 라운드 정산 기준 장수 (2를 든 만큼 늘어난 남은 장수). 정산 연출에서 누가 누구에게 주는지 계산용, 라운드 중에는 0
    public int settleCount;
    // 프로필 이미지 번호 (유저 DB의 imageNum). 클라이언트 LexioUI.profileSprites의 인덱스로 씀
    public int profileImage;

    public bool Equals(SeatInfo other)
    {
        return nickname == other.nickname && chips == other.chips && tileCount == other.tileCount && isAi == other.isAi && isReady == other.isReady && settleCount == other.settleCount && profileImage == other.profileImage;
    }

    public override bool Equals(object obj)
    {
        bool result = false;
        if (obj is SeatInfo other)
        {
            result = Equals(other);
        }
        return result;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(nickname, chips, tileCount, isAi, isReady, settleCount, profileImage);
    }
}
