using System;


[Serializable]
public class UserInfo
{
    public string userID;//유저 아이디
    [NonSerialized] public string userPW;//유저 비밀번호
    public string userName;//유저 이름
    public int imageNum; //프로필 이미지
    public float first;//1등 횟수
    public float second; //2등 횟수
    public float third;//3등 횟수
    public float fourth; //4등 횟수
    public float playNum; //판수
    public float averageRank; //평균 등수

    public UserInfo() { }
    public UserInfo(string _id, string _pwd, string _name, int _imageNum, float _first, float _second, float _third, float _fourth, float _playNum, float _averageRank)
    {
        userID = _id;
        userPW = _pwd;
        userName = _name;
        imageNum = _imageNum;
        first = _first;
        second = _second;
        third = _third;
        fourth = _fourth;
        playNum = _playNum;
        averageRank = _averageRank;
    }
    public void InfoFirstAdd()
    {
        playNum += 1;
        first += 1;
        AverageRankNew();
    }
    public void InfoSecondAdd()
    {
        playNum += 1;
        second += 1;
        AverageRankNew();
    }
    public void InfoThirdAdd()
    {
        playNum += 1;
        third += 1;
        AverageRankNew();
    }
    public void InfoFourthAdd()
    {
        playNum += 1;
        fourth += 1;
        AverageRankNew();
    }
    public void AverageRankNew()
    {
        averageRank = (first + 2 * second + 3 * third + 4 * fourth) / playNum;
    }
}
