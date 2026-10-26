using System.Collections.Generic;
using System.IO;
using System.Threading;
using Mirror;
using UnityEngine;

// 헤드리스 서버 콘솔 명령 (서버 실행 파일을 콘솔 창으로 띄웠을 때 그 창에 입력). 서버 네트워크 매니저 오브젝트에 붙임
//  - notice 내용  : 접속해 있는 모든 클라이언트의 메인 화면 공지란에 표시. 나중에 로그인하는 사람에게도 보냄
//  - notice file  : 서버 실행 파일 옆 notice.txt 내용을 공지로 보냄 (콘솔에서 한글 입력이 깨질 때 메모장으로 써서 보냄)
//  - notice clear : 공지를 지우고 클라이언트 공지란을 원래 문구로 되돌림
//  - players      : 접속 수와 로그인한 계정 목록을 출력
// 입력은 별도 스레드에서 한 줄씩 읽고(입력을 기다리는 동안 서버가 멈추지 않게), 처리는 Update(메인 스레드)에서 함
// 에디터에는 콘솔 입력이 없으므로 동작하지 않음
public class ServerConsole : MonoBehaviour
{
    private const string NoticeCommand = "notice ";
    private const string NoticeFileName = "notice.txt";

    private readonly Queue<string> pendingLines = new Queue<string>();
    private readonly object lineLock = new object();
    // 지금 공지를 이미 받은 연결 (나중에 로그인한 연결에만 따로 보내기 위함)
    private readonly HashSet<int> noticedConnections = new HashSet<int>();
    private readonly List<int> closedConnections = new List<int>();
    private Thread readThread;
    private volatile bool isReading;
    private string currentNotice = "";

    private void Start()
    {
        if (Application.isEditor == false)
        {
            isReading = true;
            readThread = new Thread(ReadLoop);
            readThread.IsBackground = true;
            readThread.Start();
            Debug.Log("[ServerConsole] 명령: notice <내용> / notice file / notice clear / players");
        }
    }

    private void OnDestroy()
    {
        isReading = false;
    }

    // 별도 스레드: 콘솔에서 한 줄씩 읽어 큐에 넣음. 입력이 닫히면(창 없이 실행 등) 끝냄
    private void ReadLoop()
    {
        while (isReading)
        {
            string line = null;
            try
            {
                line = System.Console.ReadLine();
            }
            catch (System.Exception)
            {
                line = null;
            }

            if (line == null)
            {
                isReading = false;
            }
            else
            {
                lock (lineLock)
                {
                    pendingLines.Enqueue(line);
                }
            }
        }
    }

    private void Update()
    {
        bool hasLine = true;
        while (hasLine)
        {
            string line = null;
            lock (lineLock)
            {
                hasLine = pendingLines.Count > 0;
                if (hasLine)
                {
                    line = pendingLines.Dequeue();
                }
            }
            if (hasLine)
            {
                HandleCommand(line.Trim());
            }
        }

        SendNoticeToNewLogins();
    }

    private void HandleCommand(string line)
    {
        if (line == "players")
        {
            PrintPlayers();
        }
        else if (line == "notice clear")
        {
            SetNotice("");
        }
        else if (line == "notice file")
        {
            SendNoticeFile();
        }
        else if (line.StartsWith(NoticeCommand))
        {
            SetNotice(line.Substring(NoticeCommand.Length).Trim());
        }
        else if (line.Length > 0)
        {
            Debug.Log("[ServerConsole] 알 수 없는 명령: " + line + " (notice <내용> / notice file / notice clear / players)");
        }
    }

    // 실행 파일 옆 notice.txt를 읽어 공지로 보냄. 공지란은 한 줄로 흐르므로 여러 줄은 띄어쓰기로 이음
    // File.ReadAllText는 BOM으로 UTF-8·UTF-16(메모장 "유니코드")을 구분하고, BOM이 없으면 UTF-8로 읽음
    private void SendNoticeFile()
    {
        // 실행본의 dataPath는 "실행파일이름_Data" 폴더라서 그 부모가 실행 파일 폴더
        string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), NoticeFileName);
        string text = "";
        bool isRead = false;
        try
        {
            text = File.ReadAllText(path);
            isRead = true;
        }
        catch (System.Exception exception)
        {
            Debug.Log("[ServerConsole] " + path + " 을(를) 읽지 못함: " + exception.Message);
        }

        if (isRead)
        {
            string[] lines = text.Split(new char[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            List<string> parts = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                string part = lines[i].Trim();
                if (part.Length > 0)
                {
                    parts.Add(part);
                }
            }
            string notice = string.Join(" ", parts);
            if (notice.Length > 0)
            {
                SetNotice(notice);
            }
            else
            {
                Debug.Log("[ServerConsole] " + NoticeFileName + " 이(가) 비어 있음 (공지를 지우려면 notice clear)");
            }
        }
    }

    // 접속해 있는 모든 연결(로그인 전 포함)에 보내고, 로그인한 연결은 받은 것으로 기록
    private void SetNotice(string text)
    {
        currentNotice = text;
        noticedConnections.Clear();
        if (NetworkServer.active)
        {
            NetworkServer.SendToAll(new ServerNoticeMessage { text = text });
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn.isAuthenticated)
                {
                    noticedConnections.Add(conn.connectionId);
                }
            }
        }

        if (text.Length > 0)
        {
            Debug.Log("[ServerConsole] 공지 보냄 (" + NetworkServer.connections.Count + "명 접속): " + text);
        }
        else
        {
            Debug.Log("[ServerConsole] 공지 지움 (" + NetworkServer.connections.Count + "명 접속)");
        }
    }

    // 공지가 있는 동안 새로 로그인한 연결에 한 번 보냄
    private void SendNoticeToNewLogins()
    {
        if (currentNotice.Length > 0 && NetworkServer.active)
        {
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn.isAuthenticated && noticedConnections.Contains(conn.connectionId) == false)
                {
                    conn.Send(new ServerNoticeMessage { text = currentNotice });
                    noticedConnections.Add(conn.connectionId);
                }
            }

            // 끊긴 연결은 기록에서 지움
            closedConnections.Clear();
            foreach (int id in noticedConnections)
            {
                if (NetworkServer.connections.ContainsKey(id) == false)
                {
                    closedConnections.Add(id);
                }
            }
            for (int i = 0; i < closedConnections.Count; i++)
            {
                noticedConnections.Remove(closedConnections[i]);
            }
        }
    }

    private void PrintPlayers()
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.Append("[ServerConsole] 접속 ").Append(NetworkServer.connections.Count).Append("명");
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            builder.Append("\n  #").Append(conn.connectionId).Append(' ');
            if (conn.authenticationData is PlayerProfile profile)
            {
                builder.Append(profile.userId).Append(" (").Append(profile.nickname).Append(')');
            }
            else
            {
                builder.Append("(로그인 전)");
            }
        }
        Debug.Log(builder.ToString());
    }
}
