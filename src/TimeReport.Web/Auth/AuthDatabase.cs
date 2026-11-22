using System.Globalization;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using TimeReport.Services;
using TimeReport.Web.Configuration;

namespace TimeReport.Web.Auth;

public sealed class AuthDatabaseException : Exception
{
    public AuthDatabaseException(string message) : base(message)
    {
    }

    public AuthDatabaseException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>
/// Encrypted account, MFA and audit database. It is separate from per-user
/// time databases so account state and tenant data can be backed up and
/// reasoned about independently.
/// </summary>
public sealed class AuthDatabase
{
    private const int SchemaVersion = 1;

    private const string UserSelect = """
        SELECT
            u.id, u.username, u.display_name, u.password_hash, u.is_admin, u.disabled,
            u.must_change_password, u.totp_secret, u.totp_confirmed, u.totp_last_step,
            u.language, u.theme, u.timezone_id, u.overtime_daily_minutes,
            u.overtime_weekly_minutes, u.backup_retention_count, u.security_stamp,
            u.created_at, u.updated_at,
            (SELECT COUNT(*) FROM recovery_codes AS r
             WHERE r.user_id = u.id AND r.used_at IS NULL)
        FROM users AS u
        """;

    private readonly string _path;
    private readonly byte[] _key;
    private readonly object _initializeLock = new();
    private bool _initialized;

    static AuthDatabase()
    {
        Batteries_V2.Init();
    }

    public AuthDatabase(string path, string base64Key)
    {
        _path = System.IO.Path.GetFullPath(path);
        _key = Convert.FromBase64String(base64Key);
        if (_key.Length != 32)
        {
            throw new ArgumentException("The authentication database key must be 32 bytes.");
        }
    }

    public string FilePath => _path;

    public void Initialize()
    {
        lock (_initializeLock)
        {
            if (_initialized)
            {
                return;
            }

            var directory = System.IO.Path.GetDirectoryName(_path)
                ?? throw new AuthDatabaseException("The authentication database path has no directory.");
            WebPaths.EnsurePrivateDirectory(directory);

            if (File.Exists(_path) &&
                new FileInfo(_path).Length > 0 &&
                IsPlaintextDatabase(_path))
            {
                throw new AuthDatabaseException(
                    "The authentication database is not encrypted. " +
                    "Remove the development database or restore an encrypted copy.");
            }

            try
            {
                using var connection = OpenConnection();
                Execute(connection, "PRAGMA journal_mode = WAL");
                Execute(connection, "PRAGMA synchronous = NORMAL");
                CreateSchema(connection);
                AppPaths.SecureFile(_path);
                _initialized = true;
            }
            catch (SqliteException exception)
            {
                throw new AuthDatabaseException(
                    "The authentication database could not be opened. " +
                    "The master key may be missing or incorrect.",
                    exception);
            }
        }
    }

    public void Ping()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.ExecuteScalar();
    }

    public int CountUsers()
    {
        using var connection = OpenConnection();
        return Convert.ToInt32(Scalar(connection, "SELECT COUNT(*) FROM users"), CultureInfo.InvariantCulture);
    }

    public AuthUser? GetUserByUsername(string username)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = UserSelect + " WHERE u.username = $username LIMIT 1";
        command.Parameters.AddWithValue("$username", username.Trim());
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public AuthUser? GetUserById(string id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = UserSelect + " WHERE u.id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public IReadOnlyList<AuthUser> ListUsers()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = UserSelect + " ORDER BY u.username COLLATE NOCASE";
        using var reader = command.ExecuteReader();
        var users = new List<AuthUser>();
        while (reader.Read())
        {
            users.Add(ReadUser(reader));
        }
        return users;
    }

    public int CountActiveAdmins(string? excludeUserId = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM users
            WHERE is_admin = 1 AND disabled = 0
              AND ($exclude IS NULL OR id <> $exclude)
            """;
        command.Parameters.AddWithValue("$exclude", (object?)excludeUserId ?? DBNull.Value);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public void CreateFirstAdmin(AuthUser user)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using (var countCommand = connection.CreateCommand())
            {
                countCommand.Transaction = transaction;
                countCommand.CommandText = "SELECT COUNT(*) FROM users";
                var count = Convert.ToInt32(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
                if (count != 0)
                {
                    throw new AuthDatabaseException("Setup has already been completed.");
                }
            }

            InsertUser(connection, transaction, user);
            transaction.Commit();
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch (InvalidOperationException)
            {
            }
            throw;
        }
    }

    public void CreateUser(AuthUser user)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            InsertUser(connection, transaction, user);
            transaction.Commit();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new AuthDatabaseException("That username is already in use.", exception);
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch (InvalidOperationException)
            {
            }
            throw;
        }
    }

    public void UpdateUser(AuthUser user)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE users SET
                display_name = $displayName,
                password_hash = $passwordHash,
                is_admin = $isAdmin,
                disabled = $disabled,
                must_change_password = $mustChange,
                totp_secret = $totpSecret,
                totp_confirmed = $totpConfirmed,
                totp_last_step = $totpLastStep,
                language = $language,
                theme = $theme,
                timezone_id = $timezone,
                overtime_daily_minutes = $overtimeDaily,
                overtime_weekly_minutes = $overtimeWeekly,
                backup_retention_count = $backupRetention,
                security_stamp = $securityStamp,
                updated_at = $updatedAt
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$displayName", user.DisplayName);
        command.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$isAdmin", user.IsAdmin ? 1 : 0);
        command.Parameters.AddWithValue("$disabled", user.Disabled ? 1 : 0);
        command.Parameters.AddWithValue("$mustChange", user.MustChangePassword ? 1 : 0);
        command.Parameters.AddWithValue("$totpSecret", (object?)user.TotpSecret ?? DBNull.Value);
        command.Parameters.AddWithValue("$totpConfirmed", user.TotpConfirmed ? 1 : 0);
        command.Parameters.AddWithValue("$totpLastStep", user.TotpLastStep);
        command.Parameters.AddWithValue("$language", user.Language);
        command.Parameters.AddWithValue("$theme", user.Theme);
        command.Parameters.AddWithValue("$timezone", user.TimeZoneId);
        command.Parameters.AddWithValue("$overtimeDaily", user.OvertimeDailyMinutes);
        command.Parameters.AddWithValue("$overtimeWeekly", user.OvertimeWeeklyMinutes);
        command.Parameters.AddWithValue("$backupRetention", user.BackupRetentionCount);
        command.Parameters.AddWithValue("$securityStamp", user.SecurityStamp);
        command.Parameters.AddWithValue("$updatedAt", FormatDate(user.UpdatedAtUtc));
        command.Parameters.AddWithValue("$id", user.Id);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new AuthDatabaseException("The user no longer exists.");
        }
    }

    public void SetPassword(string userId, string passwordHash, bool mustChange, string securityStamp)
    {
        ExecuteUpdateAndRevokePending(
            """
            UPDATE users SET password_hash = $passwordHash,
                must_change_password = $mustChange,
                security_stamp = $stamp, updated_at = $now
            WHERE id = $id
            """,
            command =>
            {
                command.Parameters.AddWithValue("$passwordHash", passwordHash);
                command.Parameters.AddWithValue("$mustChange", mustChange ? 1 : 0);
                command.Parameters.AddWithValue("$stamp", securityStamp);
                command.Parameters.AddWithValue("$now", FormatDate(DateTimeOffset.UtcNow));
                command.Parameters.AddWithValue("$id", userId);
            },
            userId);
    }

    public void SetTotpSecret(string userId, string protectedSecret)
    {
        ExecuteUpdate(
            """
            UPDATE users SET totp_secret = $secret, totp_confirmed = 0,
                totp_last_step = 0, updated_at = $now
            WHERE id = $id
            """,
            command =>
            {
                command.Parameters.AddWithValue("$secret", protectedSecret);
                command.Parameters.AddWithValue("$now", FormatDate(DateTimeOffset.UtcNow));
                command.Parameters.AddWithValue("$id", userId);
            });
    }

    public void ConfirmTotp(string userId, long step, string securityStamp)
    {
        ExecuteUpdate(
            """
            UPDATE users SET totp_confirmed = 1, totp_last_step = $step,
                security_stamp = $stamp, updated_at = $now
            WHERE id = $id AND totp_confirmed = 0
            """,
            command =>
            {
                command.Parameters.AddWithValue("$step", step);
                command.Parameters.AddWithValue("$stamp", securityStamp);
                command.Parameters.AddWithValue("$now", FormatDate(DateTimeOffset.UtcNow));
                command.Parameters.AddWithValue("$id", userId);
            });
    }

    public void ResetTotp(string userId, string securityStamp)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    UPDATE users SET totp_secret = NULL, totp_confirmed = 0,
                        totp_last_step = 0, security_stamp = $stamp, updated_at = $now
                    WHERE id = $id
                    """;
                command.Parameters.AddWithValue("$stamp", securityStamp);
                command.Parameters.AddWithValue("$now", FormatDate(DateTimeOffset.UtcNow));
                command.Parameters.AddWithValue("$id", userId);
                if (command.ExecuteNonQuery() == 0)
                {
                    throw new AuthDatabaseException("The user no longer exists.");
                }
            }

            using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM recovery_codes WHERE user_id = $id";
                delete.Parameters.AddWithValue("$id", userId);
                delete.ExecuteNonQuery();
            }

            using (var pending = connection.CreateCommand())
            {
                pending.Transaction = transaction;
                pending.CommandText = "DELETE FROM pending_mfa WHERE user_id = $id";
                pending.Parameters.AddWithValue("$id", userId);
                pending.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch (InvalidOperationException)
            {
            }
            throw;
        }
    }

    public void SetDisabled(string userId, bool disabled, string securityStamp)
    {
        ExecuteUpdateAndRevokePending(
            """
            UPDATE users SET disabled = $disabled, security_stamp = $stamp, updated_at = $now
            WHERE id = $id
            """,
            command =>
            {
                command.Parameters.AddWithValue("$disabled", disabled ? 1 : 0);
                command.Parameters.AddWithValue("$stamp", securityStamp);
                command.Parameters.AddWithValue("$now", FormatDate(DateTimeOffset.UtcNow));
                command.Parameters.AddWithValue("$id", userId);
            },
            userId);
    }

    public void SetAdmin(string userId, bool isAdmin, string securityStamp)
    {
        ExecuteUpdateAndRevokePending(
            """
            UPDATE users SET is_admin = $isAdmin, security_stamp = $stamp, updated_at = $now
            WHERE id = $id
            """,
            command =>
            {
                command.Parameters.AddWithValue("$isAdmin", isAdmin ? 1 : 0);
                command.Parameters.AddWithValue("$stamp", securityStamp);
                command.Parameters.AddWithValue("$now", FormatDate(DateTimeOffset.UtcNow));
                command.Parameters.AddWithValue("$id", userId);
            },
            userId);
    }

    public void UpdatePreferences(
        string userId,
        string displayName,
        string language,
        string theme,
        string timeZoneId,
        int overtimeDailyMinutes,
        int overtimeWeeklyMinutes,
        int backupRetentionCount)
    {
        ExecuteUpdate(
            """
            UPDATE users SET display_name = $displayName, language = $language,
                theme = $theme, timezone_id = $timezone,
                overtime_daily_minutes = $overtimeDaily,
                overtime_weekly_minutes = $overtimeWeekly,
                backup_retention_count = $backupRetention,
                updated_at = $now
            WHERE id = $id
            """,
            command =>
            {
                command.Parameters.AddWithValue("$displayName", displayName);
                command.Parameters.AddWithValue("$language", language);
                command.Parameters.AddWithValue("$theme", theme);
                command.Parameters.AddWithValue("$timezone", timeZoneId);
                command.Parameters.AddWithValue("$overtimeDaily", overtimeDailyMinutes);
                command.Parameters.AddWithValue("$overtimeWeekly", overtimeWeeklyMinutes);
                command.Parameters.AddWithValue("$backupRetention", backupRetentionCount);
                command.Parameters.AddWithValue("$now", FormatDate(DateTimeOffset.UtcNow));
                command.Parameters.AddWithValue("$id", userId);
            });
    }

    public bool TryAdvanceTotpStep(string userId, long step)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE users SET totp_last_step = $step, updated_at = $now
            WHERE id = $id AND totp_last_step < $step
            """;
        command.Parameters.AddWithValue("$step", step);
        command.Parameters.AddWithValue("$now", FormatDate(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$id", userId);
        return command.ExecuteNonQuery() > 0;
    }

    public void DeleteUser(string userId)
    {
        ExecuteUpdate(
            "DELETE FROM users WHERE id = $id",
            command => command.Parameters.AddWithValue("$id", userId));
    }

    public void ReplaceRecoveryCodes(string userId, IEnumerable<string> hashes)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM recovery_codes WHERE user_id = $id";
                delete.Parameters.AddWithValue("$id", userId);
                delete.ExecuteNonQuery();
            }

            foreach (var hash in hashes)
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO recovery_codes (user_id, code_hash)
                    VALUES ($userId, $hash)
                    """;
                insert.Parameters.AddWithValue("$userId", userId);
                insert.Parameters.AddWithValue("$hash", hash);
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch (InvalidOperationException)
            {
            }
            throw;
        }
    }

    public bool ConsumeRecoveryCode(string userId, string codeHash)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE recovery_codes SET used_at = $now
            WHERE id = (
                SELECT id FROM recovery_codes
                WHERE user_id = $userId AND code_hash = $hash AND used_at IS NULL
                LIMIT 1
            )
            RETURNING id
            """;
        command.Parameters.AddWithValue("$now", FormatDate(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$hash", codeHash);
        return command.ExecuteScalar() is not null;
    }

    public int CountUnusedRecoveryCodes(string userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM recovery_codes
            WHERE user_id = $userId AND used_at IS NULL
            """;
        command.Parameters.AddWithValue("$userId", userId);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public void CreatePendingMfa(string tokenHash, string userId, DateTimeOffset expiresAtUtc)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = """
                    DELETE FROM pending_mfa
                    WHERE user_id = $userId OR expires_at < $now
                    """;
                delete.Parameters.AddWithValue("$userId", userId);
                delete.Parameters.AddWithValue("$now", FormatDate(DateTimeOffset.UtcNow));
                delete.ExecuteNonQuery();
            }

            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO pending_mfa (token_hash, user_id, created_at, expires_at, attempts)
                    VALUES ($hash, $userId, $now, $expires, 0)
                    """;
                insert.Parameters.AddWithValue("$hash", tokenHash);
                insert.Parameters.AddWithValue("$userId", userId);
                insert.Parameters.AddWithValue("$now", FormatDate(DateTimeOffset.UtcNow));
                insert.Parameters.AddWithValue("$expires", FormatDate(expiresAtUtc));
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch (InvalidOperationException)
            {
            }
            throw;
        }
    }

    public PendingMfa? GetPendingMfa(string tokenHash)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT token_hash, user_id, created_at, expires_at, attempts
            FROM pending_mfa WHERE token_hash = $hash LIMIT 1
            """;
        command.Parameters.AddWithValue("$hash", tokenHash);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var pending = new PendingMfa(
            reader.GetString(0),
            reader.GetString(1),
            ParseDate(reader.GetString(2)),
            ParseDate(reader.GetString(3)),
            reader.GetInt32(4));
        if (pending.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            DeletePendingMfa(tokenHash);
            return null;
        }
        return pending;
    }

    public int IncrementPendingMfaAttempts(string tokenHash)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE pending_mfa SET attempts = attempts + 1
            WHERE token_hash = $hash
            RETURNING attempts
            """;
        command.Parameters.AddWithValue("$hash", tokenHash);
        var result = command.ExecuteScalar();
        return result is null ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    public void DeletePendingMfa(string tokenHash)
    {
        ExecuteUpdate(
            "DELETE FROM pending_mfa WHERE token_hash = $hash",
            command => command.Parameters.AddWithValue("$hash", tokenHash));
    }

    public void Audit(string? actor, string action, string? detail, string? ipAddress)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO auth_audit (at, actor, action, detail, ip_address)
            VALUES ($at, $actor, $action, $detail, $ip)
            """;
        command.Parameters.AddWithValue("$at", FormatDate(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$actor", (object?)actor ?? DBNull.Value);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$detail", (object?)detail ?? DBNull.Value);
        command.Parameters.AddWithValue("$ip", (object?)ipAddress ?? DBNull.Value);
        command.ExecuteNonQuery();
        PruneAudit(connection);
    }

    public IReadOnlyList<AuthAuditEntry> ListAudit(int limit = 200)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, at, actor, action, detail, ip_address
            FROM auth_audit ORDER BY id DESC LIMIT $limit
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));
        using var reader = command.ExecuteReader();
        var entries = new List<AuthAuditEntry>();
        while (reader.Read())
        {
            entries.Add(new AuthAuditEntry(
                reader.GetInt64(0),
                ParseDate(reader.GetString(1)),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }
        return entries;
    }

    private static void PruneAudit(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM auth_audit WHERE id <= (
                SELECT id FROM auth_audit ORDER BY id DESC LIMIT 1 OFFSET 9999
            )
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = _path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                ForeignKeys = true,
                DefaultTimeout = 5,
                Pooling = false,
            }.ToString());
        connection.Open();
        try
        {
            ApplyKey(connection);
            Execute(connection, "PRAGMA cipher_memory_security = ON");
            Execute(connection, "PRAGMA trusted_schema = OFF");
            Execute(connection, "PRAGMA foreign_keys = ON");
            Execute(connection, "PRAGMA busy_timeout = 5000");
            Execute(connection, "PRAGMA secure_delete = ON");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private void ApplyKey(SqliteConnection connection)
    {
        var hex = Convert.ToHexString(_key);
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA key = \"x'{hex}'\";";
        command.ExecuteNonQuery();
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        var version = Convert.ToInt32(
            Scalar(connection, "PRAGMA user_version"),
            CultureInfo.InvariantCulture);
        if (version > SchemaVersion)
        {
            throw new AuthDatabaseException(
                $"The authentication database uses schema version {version}, " +
                $"but this build supports up to version {SchemaVersion}.");
        }

        using var transaction = connection.BeginTransaction();
        foreach (var statement in Schema)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"PRAGMA user_version = {SchemaVersion}";
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private static readonly string[] Schema =
    [
        """
        CREATE TABLE IF NOT EXISTS users (
            id TEXT PRIMARY KEY,
            username TEXT NOT NULL COLLATE NOCASE UNIQUE,
            display_name TEXT NOT NULL,
            password_hash TEXT NOT NULL,
            is_admin INTEGER NOT NULL DEFAULT 0 CHECK (is_admin IN (0, 1)),
            disabled INTEGER NOT NULL DEFAULT 0 CHECK (disabled IN (0, 1)),
            must_change_password INTEGER NOT NULL DEFAULT 0 CHECK (must_change_password IN (0, 1)),
            totp_secret TEXT,
            totp_confirmed INTEGER NOT NULL DEFAULT 0 CHECK (totp_confirmed IN (0, 1)),
            totp_last_step INTEGER NOT NULL DEFAULT 0,
            language TEXT NOT NULL DEFAULT 'en',
            theme TEXT NOT NULL DEFAULT 'dark',
            timezone_id TEXT NOT NULL DEFAULT 'UTC',
            overtime_daily_minutes INTEGER NOT NULL DEFAULT 0,
            overtime_weekly_minutes INTEGER NOT NULL DEFAULT 0,
            backup_retention_count INTEGER NOT NULL DEFAULT 20,
            security_stamp TEXT NOT NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS recovery_codes (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            code_hash TEXT NOT NULL,
            used_at TEXT
        )
        """,
        "CREATE INDEX IF NOT EXISTS idx_recovery_codes_user ON recovery_codes(user_id, used_at)",
        """
        CREATE TABLE IF NOT EXISTS pending_mfa (
            token_hash TEXT PRIMARY KEY,
            user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            created_at TEXT NOT NULL,
            expires_at TEXT NOT NULL,
            attempts INTEGER NOT NULL DEFAULT 0
        )
        """,
        "CREATE INDEX IF NOT EXISTS idx_pending_mfa_user ON pending_mfa(user_id)",
        """
        CREATE TABLE IF NOT EXISTS auth_audit (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            at TEXT NOT NULL,
            actor TEXT,
            action TEXT NOT NULL,
            detail TEXT,
            ip_address TEXT
        )
        """,
        "CREATE INDEX IF NOT EXISTS idx_auth_audit_at ON auth_audit(id DESC)",
    ];

    private static void InsertUser(SqliteConnection connection, SqliteTransaction transaction, AuthUser user)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO users (
                id, username, display_name, password_hash, is_admin, disabled,
                must_change_password, totp_secret, totp_confirmed, totp_last_step,
                language, theme, timezone_id, overtime_daily_minutes,
                overtime_weekly_minutes, backup_retention_count, security_stamp,
                created_at, updated_at
            ) VALUES (
                $id, $username, $displayName, $passwordHash, $isAdmin, $disabled,
                $mustChange, $totpSecret, $totpConfirmed, $totpLastStep,
                $language, $theme, $timezone, $overtimeDaily,
                $overtimeWeekly, $backupRetention, $securityStamp,
                $createdAt, $updatedAt
            )
            """;
        command.Parameters.AddWithValue("$id", user.Id);
        command.Parameters.AddWithValue("$username", user.Username);
        command.Parameters.AddWithValue("$displayName", user.DisplayName);
        command.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$isAdmin", user.IsAdmin ? 1 : 0);
        command.Parameters.AddWithValue("$disabled", user.Disabled ? 1 : 0);
        command.Parameters.AddWithValue("$mustChange", user.MustChangePassword ? 1 : 0);
        command.Parameters.AddWithValue("$totpSecret", (object?)user.TotpSecret ?? DBNull.Value);
        command.Parameters.AddWithValue("$totpConfirmed", user.TotpConfirmed ? 1 : 0);
        command.Parameters.AddWithValue("$totpLastStep", user.TotpLastStep);
        command.Parameters.AddWithValue("$language", user.Language);
        command.Parameters.AddWithValue("$theme", user.Theme);
        command.Parameters.AddWithValue("$timezone", user.TimeZoneId);
        command.Parameters.AddWithValue("$overtimeDaily", user.OvertimeDailyMinutes);
        command.Parameters.AddWithValue("$overtimeWeekly", user.OvertimeWeeklyMinutes);
        command.Parameters.AddWithValue("$backupRetention", user.BackupRetentionCount);
        command.Parameters.AddWithValue("$securityStamp", user.SecurityStamp);
        command.Parameters.AddWithValue("$createdAt", FormatDate(user.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAt", FormatDate(user.UpdatedAtUtc));
        command.ExecuteNonQuery();
    }

    private static AuthUser ReadUser(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Username = reader.GetString(1),
        DisplayName = reader.GetString(2),
        PasswordHash = reader.GetString(3),
        IsAdmin = reader.GetInt32(4) != 0,
        Disabled = reader.GetInt32(5) != 0,
        MustChangePassword = reader.GetInt32(6) != 0,
        TotpSecret = reader.IsDBNull(7) ? null : reader.GetString(7),
        TotpConfirmed = reader.GetInt32(8) != 0,
        TotpLastStep = reader.GetInt64(9),
        Language = reader.GetString(10),
        Theme = reader.GetString(11),
        TimeZoneId = reader.GetString(12),
        OvertimeDailyMinutes = reader.GetInt32(13),
        OvertimeWeeklyMinutes = reader.GetInt32(14),
        BackupRetentionCount = reader.GetInt32(15),
        SecurityStamp = reader.GetString(16),
        CreatedAtUtc = ParseDate(reader.GetString(17)),
        UpdatedAtUtc = ParseDate(reader.GetString(18)),
        UnusedRecoveryCodes = reader.GetInt32(19),
    };

    private void ExecuteUpdate(string sql, Action<SqliteCommand> bind)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind(command);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new AuthDatabaseException("The user no longer exists.");
        }
    }

    private void ExecuteUpdateAndRevokePending(
        string sql,
        Action<SqliteCommand> bind,
        string userId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                bind(command);
                if (command.ExecuteNonQuery() == 0)
                {
                    throw new AuthDatabaseException("The user no longer exists.");
                }
            }

            using (var pending = connection.CreateCommand())
            {
                pending.Transaction = transaction;
                pending.CommandText = "DELETE FROM pending_mfa WHERE user_id = $id";
                pending.Parameters.AddWithValue("$id", userId);
                pending.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch (InvalidOperationException)
            {
            }
            throw;
        }
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static bool IsPlaintextDatabase(string path)
    {
        if (!PathSafety.IsRegularFile(path))
        {
            return false;
        }

        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[16];
        var read = stream.Read(header);
        return read == header.Length && header.SequenceEqual("SQLite format 3\0"u8);
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
