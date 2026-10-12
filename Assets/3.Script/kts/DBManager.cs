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

public class UserInfo
{
    public string userID { get; private set; } //유저 아이디
    public string userPW { get; private set; } //유저 비밀번호
    public string userName { get; private set; } //유저 이름
    public float first { get; private set; } //1등 횟수
    public float second { get; private set; } //2등 횟수
    public float third { get; private set; } //3등 횟수
    public float fourth { get; private set; } //4등 횟수
    public float playNum { get; private set; } //판수
    public float averageRank { get; private set; } //평균 등수

    public UserInfo(string _id, string _pwd, string _name, float _first, float _second, float _third, float _fourth, float _playNum, float _averageRank)
    {
        userID = _id;
        userPW = _pwd;
        userName = _name;
        first = _first;
        second = _second;
        third = _third;
        fourth = _fourth;
        playNum = _playNum;
        averageRank = _averageRank;
    }
}

public class DBManager : MonoBehaviour
{
    [SerializeField] private string DB_Path = string.Empty;
    public UserInfo info { get; private set; }

    private MySqlConnection con;
    private MySqlDataReader reader;

    private string id;
    private string pw;
    private string name;
    private float first;
    private float second;
    private float third;
    private float fourth;
    private float playNum;
    private float averageRank;

    public bool duplicationID { get; private set; } = false;

    //싱글톤 제작
    public static DBManager instance = null;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }
    
    
    private void Start()
    {
#if UNITY_EDITOR //에디터에서 실행한 상황
        DB_Path = Application.dataPath + "/Database";
#elif UNITY_STANDALONE_WIN //window빌드한 상황
        DB_Path = Application.persistentDataPath + "/Database";
#endif
        string serverinfo = ServerSet(DB_Path);
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

        string Jsonstring = File.ReadAllText(path + "/config.json");
        JsonData itemdata = JsonMapper.ToObject(Jsonstring);

        try
        {
            string ServerInfo = $"Server={itemdata[0]["IP"]};" +
                                $"Database={itemdata[0]["TableName"]};" +
                                $"Uid={itemdata[0]["ID"]};" +
                                $"Pwd={itemdata[0]["PW"]};" +
                                $"Port={itemdata[0]["Port"]};" +
                                "Charset=utf8;";

            return ServerInfo;
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
                return false;
            }
        }
        return true;
    }

    //로그인
    public bool Login(string _id, string _pw)
    {
        try
        {
            if (!Connection_Check(con))
            {
                return false;
            }

            string sqlCommand = string.Format(@"SELECT userID,userPW,userName,first,second,third,fourth,playNum,averageRank FROM userinfo WHERE userID='{0}' AND userPW='{1}';", _id, _pw);

            MySqlCommand command = new MySqlCommand(sqlCommand, con);

            reader = command.ExecuteReader();
            if (reader.HasRows) //reader의 행 유무
            {
                while (reader.Read())
                {
                    id = (reader.IsDBNull(reader.GetOrdinal("userID"))) ? string.Empty : reader["userID"].ToString();
                    pw = (reader.IsDBNull(reader.GetOrdinal("userPW"))) ? string.Empty : reader["userPW"].ToString();
                    name = (reader.IsDBNull(reader.GetOrdinal("userName"))) ? string.Empty : reader["userName"].ToString();
                    first = reader.GetFloat(reader.GetOrdinal("first")); 
                    second = reader.GetFloat(reader.GetOrdinal("second"));
                    third = reader.GetFloat(reader.GetOrdinal("third"));
                    fourth = reader.GetFloat(reader.GetOrdinal("fourth"));
                    playNum = reader.GetFloat(reader.GetOrdinal("playNum"));
                    averageRank = reader.GetFloat(reader.GetOrdinal("averageRank"));

                    if (!id.Equals(string.Empty) || !pw.Equals(string.Empty) || !name.Equals(string.Empty))
                    {//정상적으로 data를 가지고 온상황...
                        info = new UserInfo(id, pw, name, first, second, third, fourth, playNum, averageRank);
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

    //회원가입
    public bool SignUp(string _id, string _pw, string _name)
    {
        try
        {
            //1. connection 체크
            if (!Connection_Check(con))
            {
                return false;
            }

            duplicationID = false;

            string SQL_Command = string.Format(@"SELECT userID FROM userinfo WHERE UserID='{0}';", _id);
            MySqlCommand command = new MySqlCommand(SQL_Command, con);
            reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                if (!reader.IsClosed) reader.Close();
                duplicationID = true;
                return false;
            }

            if (!reader.IsClosed) reader.Close();
            SQL_Command = string.Format(@"SELECT userName FROM userinfo WHERE UserID='{0}';", _name);
            command = new MySqlCommand(SQL_Command, con);
            reader = command.ExecuteReader();

            if (reader.HasRows)
            {
                if (!reader.IsClosed) reader.Close();
                return false;
            }
            else
            {
                if (!reader.IsClosed) reader.Close();

                SQL_Command = string.Format(@"INSERT INTO userinfo (userID, userPW, userName) VALUES('{0}', '{1}', '{2}');", _id, _pw, _name);

                //쿼리문 입력
                command = new MySqlCommand(SQL_Command, con);
                reader = command.ExecuteReader(); //F9를 누른 것과 같다

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
    
    public bool NewPassword(string _id, string _pw, string _newPw)
    {
        try
        {
            //1. connection 체크
            if (!Connection_Check(con))
            {
                return false;
            }

            string SQL_Command = string.Format(@"SELECT userID,userPW,userName FROM userinfo WHERE userID='{0}' AND userPW='{1}';", _id, _pw);

            //쿼리문 입력
            MySqlCommand command = new MySqlCommand(SQL_Command, con);

            reader = command.ExecuteReader();
            if (reader.HasRows) 
            {
                while (reader.Read())//reader가 읽는 동안 반복하겠다
                {
                    //삼항 연산자를 통해
                    string id = (reader.IsDBNull(reader.GetOrdinal("userID"))) ? string.Empty : reader["userID"].ToString();
                    string pw = (reader.IsDBNull(reader.GetOrdinal("userPW"))) ? string.Empty : reader["userPW"].ToString();
                    string name = (reader.IsDBNull(reader.GetOrdinal("userName"))) ? string.Empty : reader["userName"].ToString();

                    if (!name.Equals(string.Empty) || !id.Equals(string.Empty) || !pw.Equals(string.Empty))
                    {//정상적으로 데이터를 읽어온 상황

                        if (!reader.IsClosed) reader.Close();

                        SQL_Command = string.Format(@"UPDATE userinfo SET userPW='{0}' WHERE userID='{1}' AND userPW='{2}';", _newPw, _id, _pw);
                        command = new MySqlCommand(SQL_Command, con);
                        reader = command.ExecuteReader(); //F9를 누른 것과 같다

                        if (!reader.IsClosed) reader.Close();
                        return true;
                    }
                    else
                    {//데이터를 정상적으로 읽지 못함
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

    
    public bool DeleteAccount(string _id, string _pw)
    {
        try
        {
            if (!Connection_Check(con))
            {
                return false;
            }

            string SQL_Command = string.Format(@"SELECT userID,userPW,userName FROM userinfo WHERE userID='{0}' AND userPW='{1}';", _id, _pw);

            MySqlCommand command = new MySqlCommand(SQL_Command, con);

            reader = command.ExecuteReader(); //F9를 누른 것과 같다
            if (reader.HasRows) //reader 행이 있냐? -> 조회된 데이터가 한개이상있냐?
            {
                while (reader.Read())//reader가 읽는 동안 반복하겠다
                {
                    //삼항 연산자를 통해
                    string id = (reader.IsDBNull(reader.GetOrdinal("userID"))) ? string.Empty : reader["userID"].ToString();
                    string pw = (reader.IsDBNull(reader.GetOrdinal("userPW"))) ? string.Empty : reader["userPW"].ToString();
                    string name = (reader.IsDBNull(reader.GetOrdinal("userName"))) ? string.Empty : reader["userName"].ToString();

                    if (!name.Equals(string.Empty) || !id.Equals(string.Empty) || !pw.Equals(string.Empty))
                    {//정상적으로 데이터를 읽어온 상황

                        if (!reader.IsClosed) reader.Close();

                        SQL_Command = string.Format(@"DELETE FROM userinfo WHERE userID='{0}' AND userPW='{1}';", _id, _pw);
                        command = new MySqlCommand(SQL_Command, con);
                        reader = command.ExecuteReader(); //F9를 누른 것과 같다

                        if (!reader.IsClosed) reader.Close();
                        return true;
                    }
                    else
                    {//데이터를 정상적으로 읽지 못함
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
    
}
