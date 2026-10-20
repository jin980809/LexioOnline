using System;

[Serializable]
public class GameResultInput
{
    public int gameID;        // 게임 ID
    public string userID;     // 유저 ID
    public int rank;           // 최종 등수
    public int finalCoin;      // 이번 게임의 코인 변동량 (+30, -10)

    // 현재 전적을 저장할 유저 본인의 프로필
    public int myProfile;

    // 함께 플레이한 다른 유저들의 프로필
    public int anotherProfileOne;
    public int anotherProfileTwo;
    public int anotherProfileThree;
    public int anotherProfileFour;
}