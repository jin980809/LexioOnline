using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LitJson;
using Mirror;
using Mirror.Transports.Encryption;
using MySql.Data.MySqlClient;
using UnityEngine;

// NYM 계정 경계. DB/비밀번호는 서버에서 처리하고 공개 프로필만 전송한다.
public class NYMAccountAuthenticator : NetworkAuthenticator
{
    [Header("서버 DB 설정")]
    [Tooltip("비우면 Editor는 Assets/Database/config.json, 실행본은 persistentDataPath/Database/config.json")]
    [SerializeField] string databaseConfigPath = "";
    public event Action<AccountResultMessage> ResultReceived;
    public event Action<AccountActionResult> ActionResultReceived;
    public event Action<PlayerProfile> ProfileChanged;
    public PlayerProfile Profile { get; private set; }
    public AccountOperation LastOperation { get; private set; }
    public bool TemporaryConnection { get; private set; }
    readonly HashSet<NetworkConnectionToClient> processing = new HashSet<NetworkConnectionToClient>();
    readonly HashSet<NetworkConnectionToClient> rejected = new HashSet<NetworkConnectionToClient>();
    string loginId;
    string password;
    AccountActionRequest pendingAction;

    public void SetCredentials(string id, string pw)
    {
        LastOperation = AccountOperation.Login;
        TemporaryConnection = false;
        pendingAction = default(AccountActionRequest);
        loginId = id.Trim(); password = pw;
    }

    public bool RequestAction(AccountActionRequest request)
    {
        if (NetworkClient.active && (!NetworkClient.isConnected || !NetworkClient.connection.isAuthenticated)) return false;
        LastOperation = request.operation;
        TemporaryConnection = !NetworkClient.isConnected;
        request.protocolVersion = LexioProtocol.ProtocolVersion;
        loginId = password = null;
        if (NetworkClient.isConnected) NetworkClient.Send(request);
        else { pendingAction = request; NetworkManager.singleton.StartClient(); }
        return true;
    }

    public override void OnStartServer()
    {
        NetworkServer.RegisterHandler<LoginRequestMessage>(OnLogin, false);
        NetworkServer.RegisterHandler<AccountActionRequest>(OnAction, false);
    }
    public override void OnStopServer()
    {
        NetworkServer.UnregisterHandler<LoginRequestMessage>();
        NetworkServer.UnregisterHandler<AccountActionRequest>();
        processing.Clear(); rejected.Clear(); StopAllCoroutines();
    }
    public override void OnServerAuthenticate(NetworkConnectionToClient conn) => StartCoroutine(AuthenticationTimeout(conn));
    IEnumerator AuthenticationTimeout(NetworkConnectionToClient conn)
    {
        yield return new WaitForSecondsRealtime(15f);
        if (Current(conn) && !conn.isAuthenticated) ServerReject(conn);
    }
    bool Current(NetworkConnectionToClient conn) => NetworkServer.active && NetworkServer.connections.TryGetValue(conn.connectionId, out var current) && current == conn;
    IEnumerator RejectAfterResponse(NetworkConnectionToClient conn)
    {
        rejected.Add(conn);
        yield return new WaitForSecondsRealtime(1f);
        if (Current(conn)) ServerReject(conn);
        rejected.Remove(conn);
    }

    async Task<AccountActionResult> Database(AccountActionRequest request)
    {
        try
        {
            string path = databaseConfigPath;
            if (string.IsNullOrEmpty(path)) path = Path.Combine(Application.isEditor ? Application.dataPath : Application.persistentDataPath, "Database/config.json");
            JsonData config = JsonMapper.ToObject(File.ReadAllText(path))[0];
            var builder = new MySqlConnectionStringBuilder
            {
                Server = config["IP"].ToString(), Database = config["TableName"].ToString(), UserID = config["ID"].ToString(),
                Password = config["PW"].ToString(), Port = uint.Parse(config["Port"].ToString()), CharacterSet = "utf8mb4", ConnectionTimeout = 3
            };
            string connectionString = builder.ConnectionString;
            return await Task.Run(() => Execute(request, connectionString));
        }
        catch (Exception) { return Reply(request, AccountResultCode.ServiceUnavailable); }
    }

    async void OnLogin(NetworkConnectionToClient conn, LoginRequestMessage request)
    {
        if (conn.isAuthenticated || rejected.Contains(conn) || !processing.Add(conn)) return;
        AccountActionResult result;
        try
        {
            result = request.ruleVersion != LexioProtocol.RuleVersion ? Reply(default(AccountActionRequest), AccountResultCode.VersionMismatch)
                : await Database(new AccountActionRequest { operation = AccountOperation.Login, protocolVersion = request.protocolVersion, loginId = request.loginId, password = request.password });
        }
        finally { processing.Remove(conn); }
        if (!Current(conn)) return;
        if (result.code == AccountResultCode.Success)
            foreach (var other in NetworkServer.connections.Values)
                if (other != conn && other.authenticationData is PlayerProfile profile && profile.userId == result.profile.userId)
                    result.code = AccountResultCode.AlreadyLoggedIn;
        conn.Send(new AccountResultMessage { code = result.code, profile = result.code == AccountResultCode.Success ? result.profile : default(PlayerProfile) });
        if (result.code == AccountResultCode.Success) { conn.authenticationData = result.profile; ServerAccept(conn); }
        else StartCoroutine(RejectAfterResponse(conn));
    }

    async void OnAction(NetworkConnectionToClient conn, AccountActionRequest request)
    {
        if (rejected.Contains(conn) || !processing.Add(conn)) return;
        AccountActionResult result;
        try
        {
            bool authenticated = conn.isAuthenticated && conn.authenticationData is PlayerProfile;
            if (request.operation == AccountOperation.Login || (request.operation == AccountOperation.UpdateProfile && !authenticated))
                result = Reply(request, AccountResultCode.LoginFailed);
            else if (authenticated && (request.operation == AccountOperation.ChangePassword || request.operation == AccountOperation.DeleteAccount)
                && !string.Equals(request.loginId, ((PlayerProfile)conn.authenticationData).userId, StringComparison.OrdinalIgnoreCase))
                result = Reply(request, AccountResultCode.LoginFailed);
            else
            {
                if (request.operation == AccountOperation.UpdateProfile) request.loginId = ((PlayerProfile)conn.authenticationData).userId;
                result = await Database(request);
            }
        }
        finally { processing.Remove(conn); }
        if (!Current(conn)) return;
        if (result.code == AccountResultCode.Success && request.operation == AccountOperation.UpdateProfile)
        {
            conn.authenticationData = result.profile;
            (NetworkManager.singleton as LexioNetworkManager)?.RefreshQueueProfile(conn);
        }
        conn.Send(result);
        if (!conn.isAuthenticated) StartCoroutine(RejectAfterResponse(conn));
        if (result.code == AccountResultCode.Success && (request.operation == AccountOperation.DeleteAccount || request.operation == AccountOperation.ChangePassword))
            foreach (var other in NetworkServer.connections.Values)
                if (other.authenticationData is PlayerProfile profile && string.Equals(profile.userId, request.loginId, StringComparison.OrdinalIgnoreCase)
                    && (other != conn || request.operation == AccountOperation.DeleteAccount)) StartCoroutine(RejectAfterResponse(other));
    }

    static AccountActionResult Reply(AccountActionRequest request, AccountResultCode code, PlayerProfile profile = default(PlayerProfile)) =>
        new AccountActionResult { requestId = request.requestId, operation = request.operation, code = code, profile = code == AccountResultCode.Success ? profile : default(PlayerProfile) };

    // 문자 종류 제한 없음(사용자 요청). 길이와 제어 문자만 검사. 비밀번호는 평문 저장이라 기존 userPW varchar(50)에 맞춤.
    static bool ValidText(string value, int min, int max) => value != null && value.Length >= min && value.Length <= max && !Regex.IsMatch(value, @"\p{Cc}");
    public static bool ValidId(string value) => ValidText(value, 1, 32);
    public static bool ValidNickname(string value) => ValidText(value, 2, 8) && !string.IsNullOrWhiteSpace(value);
    public static bool ValidNewPassword(string value) => ValidText(value, 1, 50);

    static AccountActionResult Execute(AccountActionRequest request, string connectionString)
    {
        if (request.protocolVersion != LexioProtocol.ProtocolVersion) return Reply(request, AccountResultCode.VersionMismatch);
        if (!Enum.IsDefined(typeof(AccountOperation), request.operation)) return Reply(request, AccountResultCode.LoginFailed);
        if (!ValidId(request.loginId)) return Reply(request, AccountResultCode.InvalidLoginId);
        bool register = request.operation == AccountOperation.Register, edit = request.operation == AccountOperation.UpdateProfile;
        if ((register || edit) && !ValidNickname(request.nickname)) return Reply(request, AccountResultCode.InvalidNickname);
        if ((register || edit) && (request.profileImage < 0 || request.profileImage > 5)) return Reply(request, AccountResultCode.InvalidProfile);
        if (request.operation != AccountOperation.CheckId && !edit && (string.IsNullOrEmpty(request.password) || request.password.Length > 128 || Regex.IsMatch(request.password, @"\p{Cc}")))
            return Reply(request, AccountResultCode.InvalidPassword);
        if (register && !ValidNewPassword(request.password) || request.operation == AccountOperation.ChangePassword && !ValidNewPassword(request.newPassword))
            return Reply(request, AccountResultCode.InvalidPassword);
        try
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                bool write = request.operation != AccountOperation.Login && request.operation != AccountOperation.CheckId;
                using (var transaction = write ? connection.BeginTransaction(System.Data.IsolationLevel.Serializable) : null)
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction; command.CommandTimeout = 3;
                    command.Parameters.AddWithValue("@id", request.loginId);
                    command.Parameters.AddWithValue("@name", request.nickname ?? "");
                    command.Parameters.AddWithValue("@image", request.profileImage);
                    if (request.operation == AccountOperation.CheckId)
                    {
                        command.CommandText = "SELECT COUNT(*) FROM userinfo WHERE userID=@id";
                        return Reply(request, Convert.ToInt32(command.ExecuteScalar()) == 0 ? AccountResultCode.Success : AccountResultCode.DuplicateLoginId);
                    }
                    if (register)
                    {
                        command.CommandText = "SELECT userID,userName FROM userinfo WHERE userID=@id OR userName=@name FOR UPDATE";
                        bool duplicateId = false, duplicateName = false;
                        using (var reader = command.ExecuteReader()) while (reader.Read())
                        {
                            duplicateId |= string.Equals(reader[0].ToString(), request.loginId, StringComparison.OrdinalIgnoreCase);
                            duplicateName |= string.Equals(reader[1].ToString(), request.nickname, StringComparison.OrdinalIgnoreCase);
                        }
                        if (duplicateId || duplicateName) return Reply(request, duplicateId ? AccountResultCode.DuplicateLoginId : AccountResultCode.DuplicateNickname);
                        command.Parameters.AddWithValue("@hash", request.password);
                        command.CommandText = "INSERT INTO userinfo(userID,userPW,userName,imageNum,first,second,third,fourth,playNum,averageRank) VALUES(@id,@hash,@name,@image,0,0,0,0,0,0)";
                        if (command.ExecuteNonQuery() != 1) return Reply(request, AccountResultCode.ServiceUnavailable);
                        transaction.Commit();
                        return Reply(request, AccountResultCode.Success, new PlayerProfile { userId = request.loginId, nickname = request.nickname, profileImage = request.profileImage });
                    }
                    command.CommandText = "SELECT userID,userPW,userName,imageNum,first,playNum,averageRank FROM userinfo WHERE userID=@id LIMIT 2" + (write ? " FOR UPDATE" : "");
                    PlayerProfile profile; string storedPassword;
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read() || reader.IsDBNull(0) || reader.IsDBNull(1)) return Reply(request, AccountResultCode.LoginFailed);
                        storedPassword = reader.GetString(1);
                        profile = new PlayerProfile
                        {
                            userId = reader.GetString(0), nickname = reader.IsDBNull(2) ? "" : reader.GetString(2).Trim(),
                            profileImage = reader.IsDBNull(3) ? 0 : Math.Max(0, Math.Min(5, Convert.ToInt32(reader[3]))),
                            wins = reader.IsDBNull(4) ? 0 : Math.Max(0, Convert.ToInt32(reader[4])),
                            gamesPlayed = reader.IsDBNull(5) ? 0 : Math.Max(0, Convert.ToInt32(reader[5])),
                            averageRank = reader.IsDBNull(6) ? 0 : Convert.ToSingle(reader[6])
                        };
                        profile.losses = Math.Max(0, profile.gamesPlayed - profile.wins);
                        if (reader.Read()) return Reply(request, AccountResultCode.LoginFailed);
                    }
                    if (!edit && !VerifyPassword(request.password, storedPassword)) return Reply(request, AccountResultCode.LoginFailed);
                    if (request.operation == AccountOperation.Login)
                        return Reply(request, string.IsNullOrEmpty(profile.nickname) || profile.nickname.Length > LexioPlayer.MaxNicknameLength || Regex.IsMatch(profile.nickname, @"\p{Cc}")
                            ? AccountResultCode.InvalidNickname : AccountResultCode.Success, profile);
                    if (edit)
                    {
                        command.CommandText = "SELECT COUNT(*) FROM userinfo WHERE userName=@name AND userID<>@id FOR UPDATE";
                        if (Convert.ToInt32(command.ExecuteScalar()) != 0) return Reply(request, AccountResultCode.DuplicateNickname);
                        command.CommandText = "UPDATE userinfo SET userName=@name,imageNum=@image WHERE userID=@id";
                        profile.nickname = request.nickname; profile.profileImage = request.profileImage;
                    }
                    else if (request.operation == AccountOperation.ChangePassword)
                    {
                        command.Parameters.AddWithValue("@hash", request.newPassword);
                        command.CommandText = "UPDATE userinfo SET userPW=@hash WHERE userID=@id";
                    }
                    else command.CommandText = "DELETE FROM userinfo WHERE userID=@id";
                    int affected = command.ExecuteNonQuery();
                    if (affected > 1 || request.operation == AccountOperation.DeleteAccount && affected != 1) return Reply(request, AccountResultCode.ServiceUnavailable);
                    transaction.Commit();
                    return Reply(request, AccountResultCode.Success, request.operation == AccountOperation.DeleteAccount ? default(PlayerProfile) : profile);
                }
            }
        }
        catch (Exception) { return Reply(request, AccountResultCode.ServiceUnavailable); }
    }

    // 신규/변경 비밀번호는 평문 저장(사용자 요청). 이전에 저장된 p1$ PBKDF2 계정 로그인만 호환 유지.
    static bool VerifyPassword(string value, string stored)
    {
        try
        {
            byte[] actual, expected;
            if (stored.StartsWith("p1$") && stored.Length == 48)
            {
                string[] parts = stored.Split('$'); byte[] salt = Convert.FromBase64String(parts[1] + "=="); expected = Convert.FromBase64String(parts[2] + "==");
                using (var hash = new Rfc2898DeriveBytes(value, salt, 600000, HashAlgorithmName.SHA256)) actual = hash.GetBytes(16);
            }
            else { actual = Encoding.UTF8.GetBytes(value); expected = Encoding.UTF8.GetBytes(stored); }
            int difference = actual.Length ^ expected.Length;
            for (int i = 0; i < Math.Min(actual.Length, expected.Length); i++) difference |= actual[i] ^ expected[i];
            return difference == 0;
        }
        catch (Exception) { return false; }
    }

    public override void OnStartClient()
    {
        NetworkClient.RegisterHandler<AccountResultMessage>(OnResult, false);
        NetworkClient.RegisterHandler<AccountActionResult>(OnActionResult, false);
    }
    public override void OnStopClient()
    {
        NetworkClient.UnregisterHandler<AccountResultMessage>(); NetworkClient.UnregisterHandler<AccountActionResult>();
        loginId = password = null; pendingAction = default(AccountActionRequest); Profile = default(PlayerProfile);
    }
    public override void OnClientAuthenticate()
    {
        var encryption = Transport.active as EncryptionTransport;
        if (encryption == null || encryption.ClientValidateServerPubKey == EncryptionTransport.ValidationMode.Off)
        {
            int requestId = pendingAction.requestId;
            loginId = password = null; pendingAction = default(AccountActionRequest);
            if (LastOperation == AccountOperation.Login) OnResult(new AccountResultMessage { code = AccountResultCode.ServiceUnavailable });
            else OnActionResult(new AccountActionResult { requestId = requestId, operation = LastOperation, code = AccountResultCode.ServiceUnavailable });
            return;
        }
        if (LastOperation == AccountOperation.Login)
        {
            var request = new LoginRequestMessage { protocolVersion = LexioProtocol.ProtocolVersion, ruleVersion = LexioProtocol.RuleVersion, loginId = loginId, password = password };
            loginId = password = null; NetworkClient.Send(request);
        }
        else { var request = pendingAction; pendingAction = default(AccountActionRequest); NetworkClient.Send(request); }
    }
    void OnResult(AccountResultMessage result)
    {
        Profile = result.code == AccountResultCode.Success ? result.profile : default(PlayerProfile);
        ResultReceived?.Invoke(result);
        if (result.code == AccountResultCode.Success) ClientAccept(); else ClientReject();
    }
    void OnActionResult(AccountActionResult result)
    {
        bool unauthenticated = NetworkClient.connection == null || !NetworkClient.connection.isAuthenticated;
        if (result.code == AccountResultCode.Success && result.operation == AccountOperation.UpdateProfile)
        {
            Profile = result.profile; ProfileChanged?.Invoke(Profile);
        }
        ActionResultReceived?.Invoke(result);
        if (unauthenticated && NetworkClient.connection != null) ClientReject();
    }
}

