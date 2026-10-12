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

    public bool Equals(SeatInfo other)
    {
        return nickname == other.nickname && chips == other.chips && tileCount == other.tileCount && isAi == other.isAi && isReady == other.isReady;
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
        return HashCode.Combine(nickname, chips, tileCount, isAi, isReady);
    }
}
