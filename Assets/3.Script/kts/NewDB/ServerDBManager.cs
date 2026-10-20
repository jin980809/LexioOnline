using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using LitJson;
using MySql.Data;
using MySql.Data.MySqlClient;
public class ServerInfo_JsonItem
{
    public string IP { get; private set; }
    public string TableName { get; private set; }
    public string ID { get; private set; }
    public string PW { get; private set; }
    public string Port { get; private set; }

    public ServerInfo_JsonItem(string _ip, string _table, string _id, string _pw, string _port)
    {
        IP = _ip;
        TableName = _table;
        ID = _id;
        PW = _pw;
        Port = _port;
    }
}
public class ServerDBManager : MonoBehaviour
{
    [SerializeField] private string DB_Path = string.Empty;

    private MySqlConnection con;
    private MySqlDataReader reader;

    private string id;
    private string pw;
    private string name;
    private int imageNum;
    private float first;
    private float playNum; //판수
    private float winRate; //1등 확률
    private float threePlayersPlayNum; //3인판 판수
    private float threePlayersFirst; //3인판 1등 횟수
    private float fourPlayersPlayNum; //4인판 판수
    private float fourPlayersFirst; //4인판 1등 횟수
    private float fivePlayersPlayNum; //5인판 판수
    private float fivePlayersFirst; //5인판 1등 횟수
    private float currentCoin; //현재 코인
    private float maxCoin; //최대 코인

    private string serverinfo;
    private string jsonstring;
    private JsonData itemdata;

    private string sqlCommand;
    private MySqlCommand command;

    public bool duplicationID { get; private set; } = false;
    public bool duplicationName { get; private set; } = false;
    

    private void Start()
    {
#if UNITY_EDITOR //에디터에서 실행한 상황
        DB_Path = Application.dataPath + "/Database";
#elif UNITY_STANDALONE_WIN //window빌드한 상황
        DB_Path = Application.persistentDataPath + "/Database";
#endif
        serverinfo = ServerSet(DB_Path);
        try
        {
            if (serverinfo.Equals(string.Empty))
            {
                Debug.Log("SQL Server JsonError");
                return;
            }
            con = new MySqlConnection(serverinfo);
            con.Open();
            Debug.Log("SQL Server connect!");

        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    private string ServerSet(string path)
    {
        CreateFile(path);

        jsonstring = File.ReadAllText(path + "/config.json");
        itemdata = JsonMapper.ToObject(jsonstring);

        try
        {
            serverinfo = $"Server={itemdata[0]["IP"]};" +
                         $"Database={itemdata[0]["TableName"]};" +
                         $"Uid={itemdata[0]["ID"]};" +
                         $"Pwd={itemdata[0]["PW"]};" +
                         $"Port={itemdata[0]["Port"]};" +
                         "Charset=utf8;";

            return serverinfo;
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
            return string.Empty;
        }
    }

    private void CreateFile(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        path = path + "/config.json";
        if (!File.Exists(path))
        {
            List<ServerInfo_JsonItem> items = new List<ServerInfo_JsonItem>();
            items.Add(new ServerInfo_JsonItem("192.168.1.48", "test", "root", "1234", "3306")); // 서버 설정 수정 <-----------------------------------------------------------
            JsonData data = JsonMapper.ToJson(items);
            File.WriteAllText(path, data.ToString());
        }
    }

    private bool Connection_Check(MySqlConnection con)
    {
        if (con.State != System.Data.ConnectionState.Open)
        {
            con.Open();
            if (con.State != System.Data.ConnectionState.Open)
            {
                Debug.Log("데이터 베이스 연결 오류");
                return false;
            }
        }
        return true;
    }

    public bool Login(string _id, string _pw, out UserInfo userinfo)
    {
        try
        {
            if (!Connection_Check(con))
            {
                userinfo = null;
                return false;
            }

            sqlCommand = string.Format(@"SELECT userID,userName,imageNum,first,winRate,playNum,threePlayersPlayNum,threePlayersFirst,fourPlayersPlayNum,fourPlayersFirst,fivePlayersPlayNum,fivePlayersFirst,currentCoin,maxCoin FROM userinfo WHERE userID='{0}' AND userPW='{1}';", _id, _pw);

            command = new MySqlCommand(sqlCommand, con);

            reader = command.ExecuteReader();
            if (reader.HasRows) //reader의 행 유무
            {
                while (reader.Read())
                {
                    id = (reader.IsDBNull(reader.GetOrdinal("userID"))) ? string.Empty : reader["userID"].ToString();
                    name = (reader.IsDBNull(reader.GetOrdinal("userName"))) ? string.Empty : reader["userName"].ToString();
                    imageNum = reader.GetInt32(reader.GetOrdinal("imageNum"));
                    first = reader.GetFloat(reader.GetOrdinal("first"));
                    winRate = reader.GetFloat(reader.GetOrdinal("winRate"));
                    playNum = reader.GetFloat(reader.GetOrdinal("playNum"));
                    threePlayersPlayNum = reader.GetFloat(reader.GetOrdinal("threePlayersPlayNum"));
                    threePlayersFirst = reader.GetFloat(reader.GetOrdinal("threePlayersFirst"));
                    fourPlayersPlayNum= reader.GetFloat(reader.GetOrdinal("fourPlayersPlayNum"));
                    fourPlayersFirst = reader.GetFloat(reader.GetOrdinal("fourPlayersFirst"));
                    fivePlayersPlayNum = reader.GetFloat(reader.GetOrdinal("fivePlayersPlayNum"));
                    fivePlayersFirst = reader.GetFloat(reader.GetOrdinal("fivePlayersFirst"));
                    currentCoin = reader.GetFloat(reader.GetOrdinal("currentCoin"));
                    maxCoin = reader.GetFloat(reader.GetOrdinal("maxCoin"));

                    if (!id.Equals(string.Empty) || !name.Equals(string.Empty))
                    {
                        userinfo = new UserInfo(id, name, imageNum, first, winRate, playNum, threePlayersPlayNum, threePlayersFirst, fourPlayersPlayNum, fourPlayersFirst, fivePlayersPlayNum, fivePlayersFirst, currentCoin, maxCoin);
                        if (!reader.IsClosed) reader.Close();
                        return true;

                    }
                    else
                    {
                        if (!reader.IsClosed) reader.Close();
                        userinfo = null;
                        return false;
                    }
                }
            }
            if (!reader.IsClosed) reader.Close();
            userinfo = null;
            return false;
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
            if (!reader.IsClosed) reader.Close();
            userinfo = null;
            return false;
        }
    }
    
    //회원가입
    public bool SignUp(string _id, string _pw, string _name, int _imageNum)
    {
        try
        {
            if (!Connection_Check(con))
            {
                return false;
            }
            duplicationID = false;
            duplicationName = false;

            sqlCommand = string.Format(@"SELECT userID FROM userinfo WHERE UserID='{0}';", _id);
            command = new MySqlCommand(sqlCommand, con);
            reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                if (!reader.IsClosed) reader.Close();
                duplicationID = true;
                return false;
            }

            if (!reader.IsClosed) reader.Close();
            sqlCommand = string.Format(@"SELECT userName FROM userinfo WHERE UserID='{0}';", _name);
            command = new MySqlCommand(sqlCommand, con);
            reader = command.ExecuteReader();

            if (reader.HasRows)
            {
                if (!reader.IsClosed) reader.Close();
                duplicationName = true;
                return false;
            }
            else
            {
                if (!reader.IsClosed) reader.Close();

                sqlCommand = string.Format(@"INSERT INTO userinfo (userID, userPW, userName, imageNum) VALUES('{0}', '{1}', '{2}', '{3}');", _id, _pw, _name, _imageNum);

                command = new MySqlCommand(sqlCommand, con);
                reader = command.ExecuteReader();

                if (!reader.IsClosed) reader.Close();
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
            if (!reader.IsClosed) reader.Close();
            return false;
        }
    }
    
    //비밀번호 변경
    public bool NewPassword(string _id, string _pw, string _newPw)
    {
        try
        {

            if (!Connection_Check(con))
            {
                return false;
            }

            sqlCommand = string.Format(@"SELECT userID,userPW,userName FROM userinfo WHERE userID='{0}' AND userPW='{1}';", _id, _pw);

            command = new MySqlCommand(sqlCommand, con);

            reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {

                    id = (reader.IsDBNull(reader.GetOrdinal("userID"))) ? string.Empty : reader["userID"].ToString();
                    pw = (reader.IsDBNull(reader.GetOrdinal("userPW"))) ? string.Empty : reader["userPW"].ToString();
                    name = (reader.IsDBNull(reader.GetOrdinal("userName"))) ? string.Empty : reader["userName"].ToString();

                    if (!name.Equals(string.Empty) || !id.Equals(string.Empty) || !pw.Equals(string.Empty))
                    {

                        if (!reader.IsClosed) reader.Close();

                        sqlCommand = string.Format(@"UPDATE userinfo SET userPW='{0}' WHERE userID='{1}' AND userPW='{2}';", _newPw, _id, _pw);
                        command = new MySqlCommand(sqlCommand, con);
                        reader = command.ExecuteReader();

                        if (!reader.IsClosed) reader.Close();
                        return true;
                    }
                    else
                    {
                        if (!reader.IsClosed) reader.Close();
                        return false;

                    }
                }
            }
            if (!reader.IsClosed) reader.Close();
            return false;
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
            if (!reader.IsClosed) reader.Close();
            return false;
        }
    }

    //회원탈퇴
    public bool DeleteAccount(string _id, string _pw)
    {
        try
        {
            if (!Connection_Check(con))
            {
                return false;
            }

            sqlCommand = string.Format(@"SELECT userID,userPW,userName FROM userinfo WHERE userID='{0}' AND userPW='{1}';", _id, _pw);

            command = new MySqlCommand(sqlCommand, con);

            reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {

                    id = (reader.IsDBNull(reader.GetOrdinal("userID"))) ? string.Empty : reader["userID"].ToString();
                    pw = (reader.IsDBNull(reader.GetOrdinal("userPW"))) ? string.Empty : reader["userPW"].ToString();
                    name = (reader.IsDBNull(reader.GetOrdinal("userName"))) ? string.Empty : reader["userName"].ToString();

                    if (!name.Equals(string.Empty) || !id.Equals(string.Empty) || !pw.Equals(string.Empty))
                    {

                        if (!reader.IsClosed) reader.Close();

                        sqlCommand = string.Format(@"DELETE FROM userinfo WHERE userID='{0}' AND userPW='{1}';", _id, _pw);
                        command = new MySqlCommand(sqlCommand, con);
                        reader = command.ExecuteReader();

                        if (!reader.IsClosed) reader.Close();
                        return true;
                    }
                    else
                    {
                        if (!reader.IsClosed) reader.Close();
                        return false;

                    }
                }
            }
            if (!reader.IsClosed) reader.Close();
            return false;
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
            if (!reader.IsClosed) reader.Close();
            return false;
        }
    }

    //닉네임 변경
    public bool NewName(string _id, string _newName, out UserInfo userinfo)
    {
        try
        {

            if (!Connection_Check(con))
            {
                userinfo = null;
                return false;
            }

            sqlCommand = string.Format(@"SELECT userID,userName FROM userinfo WHERE userID='{0}';", _id);

            command = new MySqlCommand(sqlCommand, con);

            reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {

                    id = (reader.IsDBNull(reader.GetOrdinal("userID"))) ? string.Empty : reader["userID"].ToString();
                    name = (reader.IsDBNull(reader.GetOrdinal("userName"))) ? string.Empty : reader["userName"].ToString();

                    if (!name.Equals(string.Empty) || !id.Equals(string.Empty) )
                    {

                        if (!reader.IsClosed) reader.Close();

                        sqlCommand = string.Format(@"UPDATE userinfo SET userName='{0}' WHERE userID='{1}';", _newName, _id);
                        command = new MySqlCommand(sqlCommand, con);
                        reader = command.ExecuteReader();

                        if (!reader.IsClosed) reader.Close();

                        sqlCommand = string.Format(@"SELECT userID,userName,imageNum,first,winRate,playNum,threePlayersPlayNum,threePlayersFirst,fourPlayersPlayNum,fourPlayersFirst,fivePlayersPlayNum,fivePlayersFirst,currentCoin,maxCoin FROM userinfo WHERE userID='{0}' AND userPW='{1}';", _id);

                        command = new MySqlCommand(sqlCommand, con);

                        reader = command.ExecuteReader();
                        if (reader.HasRows) //reader의 행 유무
                        {
                            while (reader.Read())
                            {
                                id = (reader.IsDBNull(reader.GetOrdinal("userID"))) ? string.Empty : reader["userID"].ToString();
                                name = (reader.IsDBNull(reader.GetOrdinal("userName"))) ? string.Empty : reader["userName"].ToString();
                                imageNum = reader.GetInt32(reader.GetOrdinal("imageNum"));
                                first = reader.GetFloat(reader.GetOrdinal("first"));
                                winRate = reader.GetFloat(reader.GetOrdinal("winRate"));
                                playNum = reader.GetFloat(reader.GetOrdinal("playNum"));
                                threePlayersPlayNum = reader.GetFloat(reader.GetOrdinal("threePlayersPlayNum"));
                                threePlayersFirst = reader.GetFloat(reader.GetOrdinal("threePlayersFirst"));
                                fourPlayersPlayNum = reader.GetFloat(reader.GetOrdinal("fourPlayersPlayNum"));
                                fourPlayersFirst = reader.GetFloat(reader.GetOrdinal("fourPlayersFirst"));
                                fivePlayersPlayNum = reader.GetFloat(reader.GetOrdinal("fivePlayersPlayNum"));
                                fivePlayersFirst = reader.GetFloat(reader.GetOrdinal("fivePlayersFirst"));
                                currentCoin = reader.GetFloat(reader.GetOrdinal("currentCoin"));
                                maxCoin = reader.GetFloat(reader.GetOrdinal("maxCoin"));

                                if (!id.Equals(string.Empty) || !name.Equals(string.Empty))
                                {
                                    userinfo = new UserInfo(id, name, imageNum, first, winRate, playNum, threePlayersPlayNum, threePlayersFirst, fourPlayersPlayNum, fourPlayersFirst, fivePlayersPlayNum, fivePlayersFirst, currentCoin, maxCoin);
                                    if (!reader.IsClosed) reader.Close();
                                    return true;

                                }
                                else
                                {
                                    if (!reader.IsClosed) reader.Close();
                                    userinfo = null;
                                    return false;
                                }
                            }
                        }
                        userinfo = null;
                        return false;
                    }
                    else
                    {
                        if (!reader.IsClosed) reader.Close();
                        userinfo = null;
                        return false;

                    }
                }
            }
            if (!reader.IsClosed) reader.Close();
            userinfo = null;
            return false;
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
            if (!reader.IsClosed) reader.Close();
            userinfo = null;
            return false;
        }
    }

    //전적 저장 메서드
    public bool SavePlayerResult(string tableName, GameResultInput result) //tableName 테이블에 테이블명 입력해서 호출
    {
        MySqlTransaction transaction = null;

        try
        {
            // 1. DB 연결 확인
            if (!Connection_Check(con))
            {
                return false;
            }

            // 2. 테이블 이름 확인
            // 테이블 이름은 허용된 값만 사용한다.
            if (tableName != "threepersons" &&
                tableName != "fourpersons" &&
                tableName != "fivepeople")
            {
                Debug.Log("잘못된 전적 테이블 이름입니다.");
                return false;
            }

            // 3. 결과 데이터 확인
            if (result == null ||
                string.IsNullOrEmpty(result.userID) ||
                result.rank < 1)
            {
                Debug.Log("게임 결과 데이터가 올바르지 않습니다.");
                return false;
            }

            // 4. 인원수에 맞는 통계 컬럼 지정
            string playColumn = "";
            string firstColumn = "";

            if (tableName == "threepersons")
            {
                playColumn = "threePlayersPlayNum";
                firstColumn = "threePlayersFirst";
            }
            else if (tableName == "fourpersons")
            {
                playColumn = "fourPlayersPlayNum";
                firstColumn = "fourPlayersFirst";
            }
            else if (tableName == "fivepeople")
            {
                playColumn = "fivePlayersPlayNum";
                firstColumn = "fivePlayersFirst";
            }

            // 5. 1등 여부 확인
            int isFirst = 0;

            if (result.rank == 1)
            {
                isFirst = 1;
            }

            // 6. 트랜잭션 시작
            transaction = con.BeginTransaction();

            // 7. 전적 저장, 3인용이라면 anotherProfileThree, anotherProfileFour에 0을 할당하면 됨
            // time에는 DB 서버의 현재 시간이 저장된다.
            sqlCommand = string.Format(@"
            INSERT INTO {0}
            (
                gameID,
                userID,
                rank,
                time,
                finalCoin,
                myProfile,
                anotherProfileOne,
                anotherProfileTwo,
                anotherProfileThree,
                anotherProfileFour
            )
            VALUES
            (
                @gameID,
                @userID,
                @rank,
                NOW(),
                @finalCoin,
                @myProfile,
                @anotherProfileOne,
                @anotherProfileTwo,
                @anotherProfileThree,
                @anotherProfileFour
            );",
                tableName);

            command = new MySqlCommand(sqlCommand, con, transaction);

            command.Parameters.AddWithValue("@gameID", result.gameID);
            command.Parameters.AddWithValue("@userID", result.userID);
            command.Parameters.AddWithValue("@rank", result.rank);
            command.Parameters.AddWithValue("@finalCoin", result.finalCoin);

            command.Parameters.AddWithValue("@myProfile", result.myProfile);
            command.Parameters.AddWithValue("@anotherProfileOne", result.anotherProfileOne);
            command.Parameters.AddWithValue("@anotherProfileTwo", result.anotherProfileTwo);
            command.Parameters.AddWithValue("@anotherProfileThree", result.anotherProfileThree);
            command.Parameters.AddWithValue("@anotherProfileFour", result.anotherProfileFour);

            int insertCount = command.ExecuteNonQuery();

            if (insertCount != 1)
            {
                throw new Exception("전적 저장에 실패했습니다.");
            }

            // 8. userinfo 통계 및 코인 갱신
            sqlCommand = string.Format(@"
            UPDATE userinfo SET
                playNum = playNum + 1,
                first = first + @isFirst,
                {0} = {0} + 1,
                {1} = {1} + @isFirst,
                currentCoin = currentCoin + @finalCoin,
                maxCoin = GREATEST(maxCoin,currentCoin + @finalCoin),
                winRate = (first + @isFirst) / (playNum + 1) * 100
                WHERE userID = @userID;",
                playColumn,
                firstColumn);

            command = new MySqlCommand(sqlCommand, con, transaction);

            command.Parameters.AddWithValue("@isFirst", isFirst);
            command.Parameters.AddWithValue("@finalCoin", result.finalCoin);
            command.Parameters.AddWithValue("@userID", result.userID);

            int updateCount = command.ExecuteNonQuery();

            if (updateCount != 1)
            {
                throw new Exception("유저 정보 갱신에 실패했습니다. userID를 확인하세요.");
            }

            // 9. 모든 작업 성공 시 저장 확정
            transaction.Commit();

            Debug.Log("전적 저장 및 통계 갱신 성공");

            return true;
        }
        catch (Exception e)
        {
            Debug.Log("SavePlayerResult 오류: " + e.Message);

            // 오류가 발생하면 전적 저장과 통계 변경을 취소한다.
            if (transaction != null)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackError)
                {
                    Debug.Log("Rollback 오류: " + rollbackError.Message);
                }
            }

            return false;
        }
        finally
        {
            // 트랜잭션 정리
            if (transaction != null)
            {
                transaction.Dispose();
            }
        }
    }

    //전적 검색 메서드
    public List<GameRecordData> GetRecentRecords(string userID, string tableName)//tableName 테이블에 테이블명 입력해서 호출
    {
        List<GameRecordData> records = new List<GameRecordData>();

        try
        {
            if (!Connection_Check(con))
            {
                return records;
            }

            if (tableName != "threepersons" &&
                tableName != "fourpersons" &&
                tableName != "fivepeople")
            {
                return records;
            }

            sqlCommand = string.Format(@"
            SELECT
                gameID,
                rank,
                time,
                finalCoin,
                myProfile,
                anotherProfileOne,
                anotherProfileTwo,
                anotherProfileThree,
                anotherProfileFour
            FROM {0}
            WHERE userID = @userID
            ORDER BY time DESC
            LIMIT 10;",
                tableName);

            command = new MySqlCommand(sqlCommand, con);
            command.Parameters.AddWithValue("@userID", userID);

            reader = command.ExecuteReader();

            while (reader.Read())
            {
                GameRecordData record = new GameRecordData();

                record.gameID = reader.GetInt32(
                    reader.GetOrdinal("gameID"));

                record.rank = reader.GetInt32(
                    reader.GetOrdinal("rank"));

                record.time = reader["time"].ToString();

                record.finalCoin = reader.GetInt32(
                    reader.GetOrdinal("finalCoin"));

                record.myProfile = reader.GetInt32(
                    reader.GetOrdinal("myProfile"));

                record.anotherProfileOne = reader.GetInt32(
                    reader.GetOrdinal("anotherProfileOne"));

                record.anotherProfileTwo = reader.GetInt32(
                    reader.GetOrdinal("anotherProfileTwo"));

                record.anotherProfileThree = reader.GetInt32(
                    reader.GetOrdinal("anotherProfileThree"));

                record.anotherProfileFour = reader.GetInt32(
                    reader.GetOrdinal("anotherProfileFour"));

                records.Add(record);
            }

            if (!reader.IsClosed)
            {
                reader.Close();
            }

            return records;
        }
        catch (Exception e)
        {
            Debug.Log("전적 조회 오류: " + e.Message);

            if (reader != null && !reader.IsClosed)
            {
                reader.Close();
            }

            return records;
        }
    }
    /*
    //등수 변경
    public void First_UP()
    {
        info.InfoFirstAdd();
        sqlCommand = string.Format(@"UPDATE userinfo SET first='{0}', averageRank='{1}', playNum='{2}' WHERE userID='{3}';", info.first, info.averageRank, info.playNum, info.userID);
        command = new MySqlCommand(sqlCommand, con);
        reader = command.ExecuteReader();
        if (!reader.IsClosed) reader.Close();
    }
    public void Second_UP()
    {
        info.InfoSecondAdd();
        sqlCommand = string.Format(@"UPDATE userinfo SET second='{0}', averageRank='{1}', playNum='{2}' WHERE userID='{3}';", info.second, info.averageRank, info.playNum, info.userID);
        command = new MySqlCommand(sqlCommand, con);
        reader = command.ExecuteReader();
        if (!reader.IsClosed) reader.Close();
    }
    public void Third_UP()
    {
        info.InfoThirdAdd();
        sqlCommand = string.Format(@"UPDATE userinfo SET third='{0}', averageRank='{1}', playNum='{2}' WHERE userID='{3}';", info.third, info.averageRank, info.playNum, info.userID);
        command = new MySqlCommand(sqlCommand, con);
        reader = command.ExecuteReader();
        if (!reader.IsClosed) reader.Close();
    }
    public void Fourth_UP()
    {
        info.InfoFourthAdd();
        sqlCommand = string.Format(@"UPDATE userinfo SET fourth='{0}', averageRank='{1}', playNum='{2}' WHERE userID='{3}';", info.fourth, info.averageRank, info.playNum, info.userID);
        command = new MySqlCommand(sqlCommand, con);
        reader = command.ExecuteReader();
        if (!reader.IsClosed) reader.Close();
    }
    */
}
