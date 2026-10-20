using System;


[Serializable]
public class UserInfo
{
    public string userID;//유저 아이디
    public string userName;//유저 이름
    public int imageNum; //프로필 이미지
    public float first;//총 1등 횟수
    public float playNum; //판수
    public float winRate; //1등 확률
    public float threePlayersPlayNum; //3인판 판수
    public float threePlayersFirst; //3인판 1등 횟수
    public float fourPlayersPlayNum; //4인판 판수
    public float fourPlayersFirst; //4인판 1등 횟수
    public float fivePlayersPlayNum; //5인판 판수
    public float fivePlayersFirst; //5인판 1등 횟수
    public float currentCoin; //현재 코인
    public float maxCoin; //최대 코인

    public UserInfo() { }
    public UserInfo(string _id, string _name, int _imageNum, float _first, float _winRate, float _playNum, float _threePlayersPlayNum, float _threePlayersFirst, float _fourPlayersPlayNum, float _fourPlayersFirst, float _fivePlayersPlayNum, float _fivePlayersFirst, float _currentCoin, float _maxCoin)
    {
        userID = _id;
        userName = _name;
        imageNum = _imageNum;
        first = _first;
        winRate = _winRate; //1등 확률
        playNum = _playNum;
        threePlayersPlayNum = _threePlayersPlayNum; //3인판 판수
        threePlayersFirst = _threePlayersFirst; //3인판 1등 횟수
        fourPlayersPlayNum = _fourPlayersPlayNum; //4인판 판수
        fourPlayersFirst = _fourPlayersFirst; //4인판 1등 횟수
        fivePlayersPlayNum = _fivePlayersPlayNum; //5인판 판수
        fivePlayersFirst = _fivePlayersFirst; //5인판 1등 횟수
        currentCoin = _currentCoin; //현재 코인
        maxCoin = _maxCoin; //최대 코인
}
    public void InfoFirstAdd()
    {
        playNum += 1;
        first += 1;
        ProbabilityWinning();
    }
   
    public void ProbabilityWinning()
    {
        winRate = first / playNum * 100;
    }
}
