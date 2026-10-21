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
    readonly HashSet<NetworkConnectionToClient> querying = new HashSet<NetworkConnectionToClient>();
    public event Action<RecordQueryResult> RecordsReceived;
    int recordRequestId;
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

    // matchSize 0=전체, 3/4/5. 반환 requestId(0=미로그인), 결과는 RecordsReceived에서 같은 requestId로 구분.
    public int RequestRecords(int matchSize)
    {
        if (!NetworkClient.isConnected || !NetworkClient.connection.isAuthenticated) return 0;
        NetworkClient.Send(new RecordQueryRequest { requestId = ++recordRequestId, matchSize = matchSize });
        return recordRequestId;
    }

    public override void OnStartServer()
    {
        NetworkServer.RegisterHandler<LoginRequestMessage>(OnLogin, false);
        NetworkServer.RegisterHandler<AccountActionRequest>(OnAction, false);
        NetworkServer.RegisterHandler<RecordQueryRequest>(OnRecordQuery);
    }
    public override void OnStopServer()
    {
        NetworkServer.UnregisterHandler<LoginRequestMessage>();
        NetworkServer.UnregisterHandler<AccountActionRequest>();
        NetworkServer.UnregisterHandler<RecordQueryRequest>();
        processing.Clear(); rejected.Clear(); querying.Clear(); StopAllCoroutines();
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

    string ConnectionString()
    {
        string path = databaseConfigPath;
        if (string.IsNullOrEmpty(path)) path = Path.Combine(Application.isEditor ? Application.dataPath : Application.persistentDataPath, "Database/config.json");
        JsonData config = JsonMapper.ToObject(File.ReadAllText(path))[0];
        var builder = new MySqlConnectionStringBuilder
        {
            Server = config["IP"].ToString(), Database = config["TableName"].ToString(), UserID = config["ID"].ToString(),
            Password = config["PW"].ToString(), Port = uint.Parse(config["Port"].ToString()), CharacterSet = "utf8mb4", ConnectionTimeout = 3
        };
        return builder.ConnectionString;
    }

    async Task<AccountActionResult> Database(AccountActionRequest request)
    {
        try
        {
            string connectionString = ConnectionString();
            return await Task.Run(() => Execute(request, connectionString));
        }
        catch (Exception) { return Reply(request, AccountResultCode.ServiceUnavailable); }
    }

    // 본인 전적만 조회: 요청 본문의 계정이 아니라 conn.authenticationData의 userId를 쓴다.
    async void OnRecordQuery(NetworkConnectionToClient conn, RecordQueryRequest request)
    {
        if (!(conn.authenticationData is PlayerProfile profile) || !querying.Add(conn)) return;
        RecordQueryResult result;
        try
        {
            string connectionString = ConnectionString();
            result = await Task.Run(() => QueryRecords(request, profile.userId, connectionString));
        }
        catch (Exception) { result = new RecordQueryResult { requestId = request.requestId, matchSize = request.matchSize, code = AccountResultCode.ServiceUnavailable }; }
        finally { querying.Remove(conn); }
        if (Current(conn)) conn.Send(result);
    }

    // [서버] 여러 계정 currentCoin에 delta를 한 트랜잭션으로 반영(입장료 -EntryFee, 복구 +EntryFee).
    // 한 명이라도 계정 행 0/2개 이상이면 아무도 바꾸지 않는다. 잔액 부족은 막지 않음(2026-10-21 사용자 결정, 음수 잔액 허용). reasons[i]=None이면 해당 계정 정상, profiles는 전원 성공 시 DB 재조회 값.
    public async Task<(RejectReason[] reasons, PlayerProfile[] profiles)> ChangeCoins(string[] userIds, int delta)
    {
        try
        {
            string connectionString = ConnectionString();
            return await Task.Run(() => ChangeCoins(userIds, delta, connectionString));
        }
        catch (Exception) { return (Unavailable(userIds.Length), null); }
    }

    static RejectReason[] Unavailable(int count)
    {
        var reasons = new RejectReason[count];
        for (int i = 0; i < count; i++) reasons[i] = RejectReason.ServiceUnavailable;
        return reasons;
    }

    static (RejectReason[] reasons, PlayerProfile[] profiles) ChangeCoins(string[] userIds, int delta, string connectionString)
    {
        var reasons = new RejectReason[userIds.Length];
        try
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction; command.CommandTimeout = 3;
                    var id = command.Parameters.Add("@id", MySqlDbType.VarChar);
                    command.Parameters.AddWithValue("@delta", delta);
                    bool ok = true;
                    for (int i = 0; i < userIds.Length; i++)
                    {
                        id.Value = userIds[i];
                        command.CommandText = "SELECT currentCoin FROM userinfo WHERE userID=@id LIMIT 2 FOR UPDATE";
                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read() || reader.IsDBNull(0)) reasons[i] = RejectReason.ServiceUnavailable;
                            if (reader.Read()) reasons[i] = RejectReason.ServiceUnavailable;
                        }
                        ok &= reasons[i] == RejectReason.None;
                    }
                    if (!ok) return (reasons, null); // 커밋 없이 Dispose → 롤백
                    var profiles = new PlayerProfile[userIds.Length];
                    for (int i = 0; i < userIds.Length; i++)
                    {
                        id.Value = userIds[i];
                        command.CommandText = "UPDATE userinfo SET currentCoin=currentCoin+@delta WHERE userID=@id";
                        if (command.ExecuteNonQuery() != 1) return (Unavailable(userIds.Length), null);
                        command.CommandText = "SELECT " + ProfileColumns + " FROM userinfo WHERE userID=@id";
                        using (var reader = command.ExecuteReader()) { reader.Read(); profiles[i] = ReadProfile(reader); }
                    }
                    transaction.Commit();
                    return (reasons, profiles);
                }
            }
        }
        catch (Exception) { return (Unavailable(userIds.Length), null); }
    }

    // [서버] 정상 완료 경기 저장(DB-20261020-04). 유효 참가자 전원의 userinfo 통계/토큰 UPDATE + 모드 전적 INSERT를 한 트랜잭션으로.
    // currentCoin은 잠근 DB 값에 그 판 남은 칩(finalChips)을 더한다(입장료는 시작 때 차감). 전적 finalCoin에는 남은 칩을 저장(2026-10-21 사용자 결정). 실패하면 전부 롤백하고 null.
    // seatIndexes[i]=userIds[i]의 시작 좌석, participantImages=시작 좌석 순 원래 참가자 전원(이탈자 포함) 색상. 잘못된 스냅샷은 색상 열 0(미기록)으로 저장.
    public async Task<PlayerProfile[]> SaveMatch(int matchSize, string[] userIds, int[] ranks, int[] finalChips, int[] seatIndexes, int[] participantImages, DateTime finishedAtUtc)
    {
        try
        {
            string connectionString = ConnectionString();
            return await Task.Run(() => SaveMatch(matchSize, userIds, ranks, finalChips, seatIndexes, participantImages, finishedAtUtc, connectionString));
        }
        catch (Exception) { return null; }
    }

    static PlayerProfile[] SaveMatch(int matchSize, string[] userIds, int[] ranks, int[] finalChips, int[] seatIndexes, int[] participantImages, DateTime finishedAtUtc, string connectionString)
    {
        if (!LexioProtocol.IsValidMatchSize(matchSize)) return null;
        bool snapshot = participantImages != null && participantImages.Length == matchSize && Array.TrueForAll(participantImages, image => image >= 0 && image <= 5);
        string mode = matchSize == 3 ? "three" : matchSize == 4 ? "four" : "five";
        try
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction; command.CommandTimeout = 3;
                    var profiles = new PlayerProfile[userIds.Length];
                    for (int i = 0; i < userIds.Length; i++)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@id", userIds[i]);
                        command.CommandText = "SELECT " + ProfileColumns + " FROM userinfo WHERE userID=@id LIMIT 2 FOR UPDATE";
                        PlayerProfile p;
                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read()) return null;
                            p = ReadProfile(reader);
                            if (reader.Read()) return null; // 같은 계정 행 2개 이상
                        }
                        bool first = ranks[i] == 1;
                        p.gamesPlayed++; if (first) p.wins++;
                        int modePlay, modeFirst;
                        if (matchSize == 3) { modePlay = ++p.threePlayNum; modeFirst = p.threeFirst += first ? 1 : 0; }
                        else if (matchSize == 4) { modePlay = ++p.fourPlayNum; modeFirst = p.fourFirst += first ? 1 : 0; }
                        else { modePlay = ++p.fivePlayNum; modeFirst = p.fiveFirst += first ? 1 : 0; }
                        p.winRate = p.wins * 100f / p.gamesPlayed;
                        p.currentCoin += finalChips[i]; // 음수 잔액도 그대로(임의 0 보정 없음)
                        p.maxCoin = Math.Max(p.maxCoin, p.currentCoin);

                        command.Parameters.AddWithValue("@first", p.wins);
                        command.Parameters.AddWithValue("@play", p.gamesPlayed);
                        command.Parameters.AddWithValue("@rate", p.winRate);
                        command.Parameters.AddWithValue("@modePlay", modePlay);
                        command.Parameters.AddWithValue("@modeFirst", modeFirst);
                        command.Parameters.AddWithValue("@coin", p.currentCoin);
                        command.Parameters.AddWithValue("@final", finalChips[i]);
                        command.Parameters.AddWithValue("@max", p.maxCoin);
                        command.Parameters.AddWithValue("@rank", ranks[i]);
                        command.Parameters.AddWithValue("@time", finishedAtUtc);
                        command.CommandText = "UPDATE userinfo SET first=@first,playNum=@play,winRate=@rate," + mode + "PlayersPlayNum=@modePlay," + mode +
                            "PlayersFirst=@modeFirst,currentCoin=@coin,maxCoin=@max WHERE userID=@id";
                        if (command.ExecuteNonQuery() != 1) return null;
                        // 색상 열은 색상+1 저장(0=미기록·기존 행 기본값). myProfile=본인, anotherProfile*=나머지 좌석 순, 빈 칸 0.
                        var stored = new int[ProfileImageColumns.Length];
                        if (snapshot)
                        {
                            stored[0] = participantImages[seatIndexes[i]] + 1;
                            for (int seat = 0, other = 1; seat < matchSize; seat++) if (seat != seatIndexes[i]) stored[other++] = participantImages[seat] + 1;
                        }
                        for (int c = 0; c < stored.Length; c++) command.Parameters.AddWithValue("@p" + c, stored[c]);
                        command.CommandText = "INSERT INTO " + RecordTable(matchSize) + "(userID,`rank`,`time`,finalCoin," + string.Join(",", ProfileImageColumns) +
                            ") VALUES(@id,@rank,@time,@final,@p0,@p1,@p2,@p3,@p4)";
                        if (command.ExecuteNonQuery() != 1) return null;
                        profiles[i] = p;
                    }
                    transaction.Commit();
                    return profiles;
                }
            }
        }
        catch (Exception) { return null; }
    }

    // [서버] 서버에서 바뀐 프로필(코인 등)을 클라에 보낸다. 기존 UpdateProfile 응답 경로 재사용, requestId 0은 UI 요청 응답과 겹치지 않는다.
    public void SendProfile(NetworkConnectionToClient conn, PlayerProfile profile)
    {
        conn.authenticationData = profile;
        conn.Send(new AccountActionResult { operation = AccountOperation.UpdateProfile, code = AccountResultCode.Success, profile = profile });
    }

    static string RecordTable(int matchSize) => matchSize == 3 ? "threepersons" : matchSize == 4 ? "fourpersons" : "fivepeople";
    static readonly string[] ProfileImageColumns = { "myProfile", "anotherProfileOne", "anotherProfileTwo", "anotherProfileThree", "anotherProfileFour" };

    // 색상+1 저장값(reader[start..start+4]) → 색상 0~5 배열. 본인 0(미기록·과거 기록)이거나 참가자 수/범위가 틀리면 null(임의 색으로 대체하지 않음).
    static int[] ReadProfileImages(MySqlDataReader reader, int start, int matchSize)
    {
        var images = new int[matchSize];
        for (int c = 0; c < ProfileImageColumns.Length; c++)
        {
            int value = Convert.ToInt32(reader[start + c]);
            if (c >= matchSize) { if (value != 0) return null; continue; }
            if (value < 1 || value > 6) return null;
            images[c] = value - 1;
        }
        return images;
    }

    // shortcut: 같은 time 행 순서는 미정, 고유키(DB-20261020-06) 계약 후 2차 정렬 추가.
    static RecordQueryResult QueryRecords(RecordQueryRequest request, string userId, string connectionString)
    {
        var result = new RecordQueryResult { requestId = request.requestId, matchSize = request.matchSize, code = AccountResultCode.InvalidRequest };
        if (request.matchSize != 0 && !LexioProtocol.IsValidMatchSize(request.matchSize)) return result;
        var sql = new StringBuilder();
        for (int size = 3; size <= 5; size++)
        {
            if (request.matchSize != 0 && request.matchSize != size) continue;
            if (sql.Length > 0) sql.Append(" UNION ALL ");
            sql.Append("SELECT ").Append(size).Append(",`rank`,`time`,finalCoin,").Append(string.Join(",", ProfileImageColumns))
                .Append(" FROM ").Append(RecordTable(size)).Append(" WHERE userID=@id");
        }
        sql.Append(" ORDER BY `time` DESC LIMIT 10");
        var records = new List<GameRecordEntry>();
        using (var connection = new MySqlConnection(connectionString))
        using (var command = connection.CreateCommand())
        {
            connection.Open();
            command.CommandTimeout = 3; command.CommandText = sql.ToString();
            command.Parameters.AddWithValue("@id", userId);
            using (var reader = command.ExecuteReader()) while (reader.Read())
            {
                // NULL 행은 보정하지 않고 실패 처리(→ServiceUnavailable)
                if (reader.IsDBNull(1) || reader.IsDBNull(2) || reader.IsDBNull(3)) throw new InvalidDataException("전적 행 NULL");
                records.Add(new GameRecordEntry
                {
                    matchSize = Convert.ToInt32(reader[0]), rank = Convert.ToInt32(reader[1]),
                    playedAtUtcTicks = DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc).Ticks, finalCoin = Convert.ToInt32(reader[3]),
                    participantProfileImages = ReadProfileImages(reader, 4, Convert.ToInt32(reader[0]))
                });
            }
        }
        result.code = AccountResultCode.Success; result.records = records.ToArray();
        return result;
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
                        // currentCoin/maxCoin은 DB 기본값(1000) 사용 후 아래 SELECT로 다시 읽어 응답에 반영
                        command.CommandText = "INSERT INTO userinfo(userID,userPW,userName,imageNum,first,playNum,winRate,threePlayersPlayNum,threePlayersFirst,fourPlayersPlayNum,fourPlayersFirst,fivePlayersPlayNum,fivePlayersFirst) VALUES(@id,@hash,@name,@image,0,0,0,0,0,0,0,0,0)";
                        if (command.ExecuteNonQuery() != 1) return Reply(request, AccountResultCode.ServiceUnavailable);
                    }
                    command.CommandText = "SELECT " + ProfileColumns + " FROM userinfo WHERE userID=@id LIMIT 2" + (write ? " FOR UPDATE" : "");
                    PlayerProfile profile; string storedPassword;
                    AccountResultCode missing = register ? AccountResultCode.ServiceUnavailable : AccountResultCode.LoginFailed;
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read() || reader.IsDBNull(0) || reader.IsDBNull(1)) return Reply(request, missing);
                        storedPassword = reader.GetString(1);
                        profile = ReadProfile(reader);
                        if (reader.Read()) return Reply(request, missing);
                    }
                    if (register)
                    {
                        transaction.Commit();
                        return Reply(request, AccountResultCode.Success, profile);
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

    const string ProfileColumns = "userID,userPW,userName,imageNum,first,playNum,winRate,threePlayersPlayNum,threePlayersFirst,fourPlayersPlayNum,fourPlayersFirst,fivePlayersPlayNum,fivePlayersFirst,currentCoin,maxCoin";

    // ProfileColumns 순서. 통계는 보정하지 않고 음수·소수면 예외(→ServiceUnavailable).
    static PlayerProfile ReadProfile(MySqlDataReader reader) => new PlayerProfile
    {
        userId = reader.GetString(0), nickname = reader.IsDBNull(2) ? "" : reader.GetString(2).Trim(),
        profileImage = reader.IsDBNull(3) ? 0 : Math.Max(0, Math.Min(5, Convert.ToInt32(reader[3]))),
        wins = Count(reader, 4), gamesPlayed = Count(reader, 5), winRate = reader.IsDBNull(6) ? 0 : Convert.ToSingle(reader[6]),
        threePlayNum = Count(reader, 7), threeFirst = Count(reader, 8), fourPlayNum = Count(reader, 9), fourFirst = Count(reader, 10),
        fivePlayNum = Count(reader, 11), fiveFirst = Count(reader, 12),
        currentCoin = Convert.ToSingle(reader[13]), maxCoin = Convert.ToSingle(reader[14])
    };
    static int Count(MySqlDataReader reader, int i)
    {
        if (reader.IsDBNull(i)) return 0;
        double value = Convert.ToDouble(reader[i]);
        if (value < 0 || value > int.MaxValue || value != Math.Floor(value)) throw new InvalidDataException("userinfo 통계 값 오류");
        return (int)value;
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
        NetworkClient.RegisterHandler<RecordQueryResult>(result => RecordsReceived?.Invoke(result));
    }
    public override void OnStopClient()
    {
        NetworkClient.UnregisterHandler<AccountResultMessage>(); NetworkClient.UnregisterHandler<AccountActionResult>(); NetworkClient.UnregisterHandler<RecordQueryResult>();
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
        if (result.code == AccountResultCode.Success) ClientAccept(); else StartCoroutine(RejectNextFrame());
    }
    void OnActionResult(AccountActionResult result)
    {
        bool unauthenticated = NetworkClient.connection == null || !NetworkClient.connection.isAuthenticated;
        if (result.code == AccountResultCode.Success && result.operation == AccountOperation.UpdateProfile)
        {
            Profile = result.profile; ProfileChanged?.Invoke(Profile);
        }
        ActionResultReceived?.Invoke(result);
        if (unauthenticated && NetworkClient.connection != null) StartCoroutine(RejectNextFrame());
    }
    // 수신 처리 도중 끊으면 같은 틱의 남은 패킷이 connection-null 오류를 낸다(QA-20261020-05). 다음 프레임에 해제.
    IEnumerator RejectNextFrame()
    {
        var connection = NetworkClient.connection;
        yield return null;
        if (connection != null && NetworkClient.connection == connection && !connection.isAuthenticated) ClientReject();
    }
}

