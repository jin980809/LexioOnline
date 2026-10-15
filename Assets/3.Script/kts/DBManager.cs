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


public class DBManager : MonoBehaviour
{
    [SerializeField] private string DB_Path = string.Empty;
    public UserInfo info { get; private set; }

    private MySqlConnection con;
    private MySqlDataReader reader;

    private string id;
    private string pw;
    private string name;
    private int imageNum;
    private float first;
    private float second;
    private float third;
    private float fourth;
    private float playNum;
    private float averageRank;
    

    string serverinfo;
    string jsonstring;
    JsonData itemdata;

    string sqlCommand;
    MySqlCommand command;

    public bool duplicationID { get; private set; } = false;
    public bool selectImage { get; private set; } = false;
    public void SelectImageON(int num)
    {
        selectImage = true;
        imageNum = num; 
    }
    public void SelectImageOFF()
    {
        selectImage = false;
    }

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

            sqlCommand = string.Format(@"SELECT userID,userPW,userName,imageNum,first,second,third,fourth,playNum,averageRank FROM userinfo WHERE userID='{0}' AND userPW='{1}';", _id, _pw);

            command = new MySqlCommand(sqlCommand, con);

            reader = command.ExecuteReader();
            if (reader.HasRows) //reader의 행 유무
            {
                while (reader.Read())
                {
                    id = (reader.IsDBNull(reader.GetOrdinal("userID"))) ? string.Empty : reader["userID"].ToString();
                    pw = (reader.IsDBNull(reader.GetOrdinal("userPW"))) ? string.Empty : reader["userPW"].ToString();
                    name = (reader.IsDBNull(reader.GetOrdinal("userName"))) ? string.Empty : reader["userName"].ToString();
                    imageNum = reader.GetInt32(reader.GetOrdinal("imageNum"));
                    first = reader.GetFloat(reader.GetOrdinal("first")); 
                    second = reader.GetFloat(reader.GetOrdinal("second"));
                    third = reader.GetFloat(reader.GetOrdinal("third"));
                    fourth = reader.GetFloat(reader.GetOrdinal("fourth"));
                    playNum = reader.GetFloat(reader.GetOrdinal("playNum"));
                    averageRank = reader.GetFloat(reader.GetOrdinal("averageRank"));

                    if (!id.Equals(string.Empty) || !pw.Equals(string.Empty) || !name.Equals(string.Empty))
                    {
                        info = new UserInfo(id, pw, name, imageNum, first, second, third, fourth, playNum, averageRank);
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
           
            if (!Connection_Check(con))
            {
                return false;
            }

            if (!selectImage)
            {
                return false;
            }

            duplicationID = false;

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
                return false;
            }
            else
            {
                if (!reader.IsClosed) reader.Close();

                sqlCommand = string.Format(@"INSERT INTO userinfo (userID, userPW, userName, imageNum) VALUES('{0}', '{1}', '{2}', '{3}');", _id, _pw, _name, imageNum);

                command = new MySqlCommand(sqlCommand, con);
                reader = command.ExecuteReader(); 

                if (!reader.IsClosed) reader.Close();
                SelectImageOFF();
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
            if (!reader.IsClosed) reader.Close();
            SelectImageOFF();
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
        sqlCommand = string.Format(@"UPDATE userinfo SET fourth='{0}', averageRank='{1}', playNum='{2}' WHERE userID='{3}';", info.fourth,info.averageRank,info.playNum, info.userID);
        command = new MySqlCommand(sqlCommand, con);
        reader = command.ExecuteReader();
        if (!reader.IsClosed) reader.Close();
    }
}
