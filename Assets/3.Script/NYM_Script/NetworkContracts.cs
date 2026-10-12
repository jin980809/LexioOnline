using System;
using Mirror;

// 서버·클라 공통 규약. 기준: 서버-클라이언트 연동명세 v1.4 / 네트워크 통합작업명세 v2.1.
// 규약을 바꾸면 ProtocolVersion을 올린다 (버전이 다르면 서버가 로그인을 거절).

public static class LexioProtocol
{
    public const int ProtocolVersion = 1;
    public const int RuleVersion = 1;

    // 명세 확정값. 기획 옵션으로 열지 않는다.
    public const int RoundCount = 5;
    public const int StartScore = 64;
    public const float TurnSeconds = 30f;
    public const int Cloud3TileId = 8;

    public static bool IsValidMatchSize(int matchSize) => matchSize >= 3 && matchSize <= 5;

    // 3인 1~9 / 4인 1~13 / 5인 1~15
    public static int MaxNumber(int matchSize) => matchSize == 3 ? 9 : matchSize == 4 ? 13 : 15;
    public static int TileCount(int matchSize) => MaxNumber(matchSize) * 4;
    public static int HandSize(int matchSize) => TileCount(matchSize) / matchSize;

    // TileId = (number-1)*4 + suit. 예: 구름3 = 8, 해3 = 11, 해2 = 7
    public static int ToTileId(int number, Suit suit) => (number - 1) * 4 + (int)suit;
    public static int GetNumber(int tileId) => tileId / 4 + 1;
    public static Suit GetSuit(int tileId) => (Suit)(tileId % 4);
    public static bool IsValidTileId(int tileId, int matchSize) => tileId >= 0 && tileId < TileCount(matchSize);
}

// ───────── 열거형 ─────────

public enum Suit { Cloud = 0, Star = 1, Moon = 2, Sun = 3 }

public enum ComboType { None, Single, Pair, Triple, Straight, Flush, FullHouse, FourOfAKind, StraightFlush }

public enum MatchPhase { Playing, RoundSettlement, GameEnd, Aborted }

public enum MatchEndReason { None, Completed, TooFewPlayers, ServerShutdown }

public enum ActionType { Play, AutoPlay, Pass, AutoPass, Leave, Settlement }

public enum AccountResultCode
{
    Success,
    LoginFailed,        // 아이디·비밀번호 오류를 구분하지 않는다
    VersionMismatch,
    InvalidLoginId,
    InvalidPassword,
    InvalidNickname,
    DuplicateLoginId,
    AlreadyLoggedIn,
    ServiceUnavailable  // DB 미준비·오류. 절대 성공으로 처리하지 않는다
}

public enum RejectReason
{
    None,
    NotLoggedIn,
    InvalidFormat,
    DuplicateRequest,
    InvalidMatchSize,
    AlreadyQueued,
    NotQueued,
    AlreadyInMatch,
    WrongMatch,
    WrongRound,
    WrongTurn,
    StaleRevision,
    NotYourTurn,
    InvalidTile,
    NotOwnedTile,
    InvalidCombo,
    CannotBeat,
    PassOnEmptyFloor,
    MatchEnded
}

// ───────── 계정 (Authenticator NetworkMessage, 로그인 전) ─────────

public struct RegisterRequestMessage : NetworkMessage
{
    public int protocolVersion;
    public int ruleVersion;
    public string loginId;
    public string password;
    public string nickname;
}

public struct LoginRequestMessage : NetworkMessage
{
    public int protocolVersion;
    public int ruleVersion;
    public string loginId;
    public string password;
}

public struct AccountResultMessage : NetworkMessage
{
    public AccountResultCode code;
    public PlayerProfile profile; // code == Success일 때만 유효
}

// 본인 전적. gamesPlayed == 0이면 averageRank는 무시하고 화면에 "—" 표시.
public struct PlayerProfile
{
    public long userId;
    public string nickname;
    public int wins;
    public int losses;
    public int gamesPlayed;
    public float averageRank;
}

// ───────── 클라 → 서버 (Command 인자) ─────────
// 같은 행동 재전송은 같은 requestId·같은 본문을 쓴다.

public struct QueueJoinRequest
{
    public int requestId;
    public int matchSize; // 3 / 4 / 5
}

public struct QueueCancelRequest
{
    public int requestId;
}

public struct PlayRequest
{
    public int requestId;
    public Guid matchId;
    public int roundId;
    public int turnId;
    public int expectedRevision;
    public int[] tileIds; // 1 / 2 / 3 / 5장
}

public struct PassRequest
{
    public int requestId;
    public Guid matchId;
    public int roundId;
    public int turnId;
    public int expectedRevision;
}

public struct SettlementReadyRequest
{
    public int requestId;
    public Guid matchId;
    public int roundId;
    public int expectedRevision;
}

public struct SnapshotRequest
{
    public int requestId;
    public Guid matchId;
}

// ───────── 서버 → 클라 ─────────

public struct QueueUpdated
{
    public int matchSize;
    public int waitingCount;
}

public struct MatchFound
{
    public Guid matchId;
    public int matchSize;
    public int ownSeat;
    public string[] nicknames; // 좌석 순서 0..N-1
}

public struct RequestResult
{
    public int requestId;
    public bool accepted;
    public RejectReason reason;
    public int revision; // 반영된 서버 상태 번호
}

public struct SeatState
{
    public string nickname;
    public int tileCount; // 남은 장수만 공개. 손패 ID는 넣지 않는다
    public int score;
    public bool connected;
}

// 공개 상태. SyncVar 한 묶음으로 같은 matchId 참가자에게만 보인다.
public struct PublicState
{
    public Guid matchId;
    public int matchSize;
    public int roundId;
    public int turnId;
    public int revision;
    public MatchPhase phase;
    public SeatState[] seats;

    public int currentSeat;
    public int passCount;
    public double turnDeadline; // NetworkTime.time 기준. 만료 판정은 서버

    public int floorSeat; // 바닥을 낸 좌석, 바닥 없으면 -1
    public ComboType floorCombo;
    public int floorRank;
    public int[] floorTileIds;

    public int[] roundDeltas; // 라운드 정산 점수 변화, 좌석 순서
    public int[] finalRanks;  // GameEnd일 때 최종 등수, 좌석 순서
    public MatchEndReason endReason;
}

// 본인 전용 상태. TargetRpc로 본인에게만. PublicState와 같은 revision일 때 함께 적용.
public struct PrivateState
{
    public Guid matchId;
    public int roundId;
    public int revision;
    public int ownSeat;
    public int[] ownTileIds;
}

public struct ClientSnapshot
{
    public PublicState publicState;
    public PrivateState privateState;
}

// 일회성 연출. (matchId, roundId, revision)별 한 번만 재생.
public struct ActionEvent
{
    public Guid matchId;
    public int roundId;
    public int revision;
    public int seat;
    public ActionType action;
    public int[] tileIds;
}
