using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace TimeReport.Data;

public class DatabaseException : Exception
{
    public DatabaseException(string message) : base(message)
    {
    }

    public DatabaseException(string message, Exception inner) : base(message, inner)
    {
    }
}

public sealed class RestoreException : DatabaseException
{
    public RestoreException(string message) : base(message)
    {
    }

    public RestoreException(string message, Exception inner) : base(message, inner)
    {
    }
}

public sealed class Database : IDisposable
{
    public const int SchemaVersion = 3;

    private static readonly string[] SchemaV1 =
    [
        """
        CREATE TABLE IF NOT EXISTS projects (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL COLLATE NOCASE UNIQUE,
            archived INTEGER NOT NULL DEFAULT 0 CHECK (archived IN (0, 1)),
            created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS entries (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            entry_date TEXT NOT NULL CHECK (entry_date = date(entry_date)),
            kind TEXT NOT NULL CHECK (kind IN ('work', 'lunch', 'away', 'xpause')),
            project_id INTEGER REFERENCES projects(id) ON DELETE RESTRICT,
            label TEXT,
            start_minute INTEGER NOT NULL CHECK (start_minute >= 0 AND start_minute < 1440),
            end_minute INTEGER NOT NULL CHECK (end_minute > start_minute AND end_minute <= 1440),
            created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
            updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')),
            CHECK (
                (kind = 'work' AND project_id IS NOT NULL AND label IS NULL)
                OR (
                    kind = 'away'
                    AND project_id IS NULL
                    AND label IS NOT NULL
                    AND length(trim(label)) > 0
                )
                OR (kind IN ('lunch', 'xpause') AND project_id IS NULL AND label IS NULL)
            )
        )
        """,
        "CREATE INDEX IF NOT EXISTS idx_entries_date ON entries(entry_date)",
        "CREATE INDEX IF NOT EXISTS idx_entries_project ON entries(project_id)",
    ];

    private static readonly string[] SchemaV2 =
    [
        """
        CREATE TABLE IF NOT EXISTS entry_types (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL COLLATE NOCASE UNIQUE,
            category TEXT NOT NULL CHECK (category IN ('work', 'lunch', 'away', 'xpause')),
            archived INTEGER NOT NULL DEFAULT 0 CHECK (archived IN (0, 1)),
            created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
        )
        """,
        "INSERT OR IGNORE INTO entry_types (name, category) VALUES ('Work', 'work')",
        "INSERT OR IGNORE INTO entry_types (name, category) VALUES ('Lunch', 'lunch')",
        "INSERT OR IGNORE INTO entry_types (name, category) VALUES ('Away', 'away')",
        "INSERT OR IGNORE INTO entry_types (name, category) VALUES ('X-Pause', 'xpause')",
        "ALTER TABLE entries ADD COLUMN entry_type_id INTEGER REFERENCES entry_types(id) ON DELETE RESTRICT",
        """
        UPDATE entries
        SET entry_type_id = (
            SELECT id FROM entry_types
            WHERE category = entries.kind
            ORDER BY id
            LIMIT 1
        )
        WHERE entry_type_id IS NULL
        """,
        "CREATE INDEX IF NOT EXISTS idx_entries_type ON entries(entry_type_id)",
    ];

    private static readonly string[] SchemaV3 =
    [
        "ALTER TABLE entries ADD COLUMN timezone_id TEXT",
        "CREATE INDEX IF NOT EXISTS idx_entries_date_start ON entries(entry_date, start_minute)",
        "CREATE INDEX IF NOT EXISTS idx_entries_type_date ON entries(entry_type_id, entry_date)",
    ];

    private static readonly Dictionary<int, string[]> Migrations = new()
    {
        [1] = SchemaV1,
        [2] = SchemaV2,
        [3] = SchemaV3,
    };

    private static readonly Dictionary<string, string[]> RequiredTablesV1 = new()
    {
        ["projects"] = ["id", "name", "archived", "created_at"],
        ["entries"] =
        [
            "id", "entry_date", "kind", "project_id", "label",
            "start_minute", "end_minute", "created_at", "updated_at",
        ],
    };

    private static readonly Dictionary<string, string[]> RequiredTablesV2 = new()
    {
        ["entry_types"] = ["id", "name", "category", "archived", "created_at"],
        ["entries"] = ["entry_type_id"],
    };

    private static readonly Dictionary<string, string[]> RequiredTablesV3 = new()
    {
        ["entries"] = ["timezone_id"],
    };

    private SqliteConnection? _connection;

    static Database()
    {
        Batteries_V2.Init();
    }

    public Database(string path, string? encryptionKey = null)
    {
        Path = System.IO.Path.GetFullPath(path);
        EncryptionKey = string.IsNullOrWhiteSpace(encryptionKey)
            ? null
            : Convert.ToBase64String(Convert.FromBase64String(encryptionKey));
    }

    public string Path { get; }

    public string? EncryptionKey { get; }

    public int BackupRetentionCount { get; set; } = 20;

    public bool IsEncrypted => EncryptionKey is not null;

    public SqliteConnection Connection =>
        _connection ?? throw new DatabaseException("The database is not open.");

    public int SchemaVersionNumber =>
        Convert.ToInt32(Scalar("PRAGMA user_version", null), System.Globalization.CultureInfo.InvariantCulture);

    public void Open()
    {
        if (_connection is not null)
        {
            return;
        }

        var directory = System.IO.Path.GetDirectoryName(Path)
            ?? throw new DatabaseException("The database path has no directory.");
        Services.AppPaths.EnsureDirectory(directory);

        if (EncryptionKey is not null && IsPlaintextDatabase(Path))
        {
            EncryptExistingDatabase(Path, EncryptionKey);
        }
        else if (EncryptionKey is null &&
                 File.Exists(Path) &&
                 new FileInfo(Path).Length > 0 &&
                 !IsPlaintextDatabase(Path))
        {
            throw new DatabaseException(
                "The database is encrypted or is not a valid SQLite database. " +
                "The database encryption key is missing.");
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            DefaultTimeout = 5,
            Pooling = false,
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        _connection = connection;

        try
        {
            ApplyKey(connection, EncryptionKey);
            Execute("PRAGMA trusted_schema = OFF");
            Execute("PRAGMA foreign_keys = ON");
            Execute("PRAGMA busy_timeout = 5000");
            Execute("PRAGMA synchronous = NORMAL");
            Migrate();
        }
        catch (SqliteException exception) when (EncryptionKey is not null)
        {
            Close();
            throw new DatabaseException(
                "The database could not be opened. The encryption key may be missing or incorrect.",
                exception);
        }
        catch
        {
            Close();
            throw;
        }

        Services.AppPaths.SecureFile(Path);
    }

    public static bool IsPlaintextFile(string path) => IsPlaintextDatabase(path);

    private static bool IsPlaintextDatabase(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[16];
        var read = stream.Read(header);
        return read == header.Length &&
               header.SequenceEqual("SQLite format 3\0"u8);
    }

    private static void ApplyKey(SqliteConnection connection, string? encryptionKey)
    {
        if (string.IsNullOrWhiteSpace(encryptionKey))
        {
            return;
        }

        var hex = Convert.ToHexString(Convert.FromBase64String(encryptionKey));
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA key = \"x'{hex}'\";";
        command.ExecuteNonQuery();
    }

    private static void ExportDatabase(
        SqliteConnection source,
        string destinationPath,
        string? destinationKey)
    {
        var escapedPath = destinationPath.Replace("'", "''");
        var keyClause = string.IsNullOrWhiteSpace(destinationKey)
            ? "KEY ''"
            : $"KEY \"x'{Convert.ToHexString(Convert.FromBase64String(destinationKey))}'\"";
        using var command = source.CreateCommand();
        command.CommandText =
            $"ATTACH DATABASE '{escapedPath}' AS timereport_export {keyClause};";
        command.ExecuteNonQuery();
        try
        {
            command.CommandText = "SELECT sqlcipher_export('timereport_export');";
            command.ExecuteNonQuery();
            var version = Convert.ToInt32(
                Scalar(source, "PRAGMA user_version", null),
                System.Globalization.CultureInfo.InvariantCulture);
            command.CommandText = $"PRAGMA timereport_export.user_version = {version};";
            command.ExecuteNonQuery();
        }
        finally
        {
            command.CommandText = "DETACH DATABASE timereport_export;";
            command.ExecuteNonQuery();
        }
    }

    private void EncryptExistingDatabase(string path, string encryptionKey)
    {
        var directory = System.IO.Path.GetDirectoryName(path)
            ?? throw new DatabaseException("The database path has no directory.");
        var backups = Services.AppPaths.BackupsDir(directory);
        Services.AppPaths.EnsureDataDir(backups);
        var backup = System.IO.Path.Combine(
            backups,
            $"pre-encryption-{Services.AppPaths.TimestampForFilename()}.db");
        var temporary = path + ".encrypted";
        TryDelete(temporary);

        try
        {
            using (var source = new SqliteConnection(
                       new SqliteConnectionStringBuilder
                       {
                           DataSource = path,
                           Mode = SqliteOpenMode.ReadWriteCreate,
                           Pooling = false,
                       }.ToString()))
            {
                source.Open();
                ExportDatabase(source, backup, encryptionKey);
                ExportDatabase(source, temporary, encryptionKey);
            }

            ValidateFile(backup, encryptionKey);
            ValidateFile(temporary, encryptionKey);
            File.Move(temporary, path, true);
            foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
            {
                TryDelete(path + suffix);
            }
            Services.AppPaths.SecureFile(path);
            Services.AppPaths.SecureFile(backup);
            PruneBackups(directory, BackupRetentionCount);
        }
        catch (Exception exception)
        {
            TryDelete(temporary);
            if (exception is DatabaseException)
            {
                throw;
            }
            throw new DatabaseException(
                $"Could not encrypt the existing database: {exception.Message}",
                exception);
        }
    }

    public void Close()
    {
        if (_connection is null)
        {
            return;
        }
        _connection.Close();
        _connection.Dispose();
        _connection = null;
    }

    public void Dispose() => Close();

    public SqliteTransaction BeginTransaction() => Connection.BeginTransaction();

    public string BackupTo(string destination) => BackupToCore(destination, validate: true);

    /// <summary>
    /// Creates a plaintext SQLite snapshot of the live database. Used only for
    /// client-side encrypted sync uploads; never leave this file behind.
    /// </summary>
    public string BackupToPlaintext(string destination)
    {
        var fullDestination = System.IO.Path.GetFullPath(destination);
        if (Services.PathSafety.IsDatabaseFile(fullDestination, Path) ||
            Services.PathSafety.AreSameFile(fullDestination, Path))
        {
            throw new DatabaseException("Choose a backup location other than the live database.");
        }

        var directory = System.IO.Path.GetDirectoryName(fullDestination)
            ?? throw new DatabaseException("The backup path has no directory.");
        Directory.CreateDirectory(directory);
        var temporary = System.IO.Path.Combine(
            directory,
            $".timereport-plain-{Guid.NewGuid():N}.db");

        try
        {
            ExportDatabase(Connection, temporary, null);
            ValidateFile(temporary);
            File.Move(temporary, fullDestination, true);
            Services.AppPaths.SecureFile(fullDestination);
            return fullDestination;
        }
        catch (Exception exception)
        {
            TryDelete(temporary);
            if (exception is DatabaseException)
            {
                throw;
            }
            throw new DatabaseException($"Could not create the backup: {exception.Message}", exception);
        }
    }

    private string BackupToCore(string destination, bool validate)
    {
        var fullDestination = System.IO.Path.GetFullPath(destination);
        if (Services.PathSafety.IsDatabaseFile(fullDestination, Path) ||
            Services.PathSafety.AreSameFile(fullDestination, Path))
        {
            throw new DatabaseException("Choose a backup location other than the live database.");
        }

        var directory = System.IO.Path.GetDirectoryName(fullDestination)
            ?? throw new DatabaseException("The backup path has no directory.");
        Directory.CreateDirectory(directory);
        var temporary = System.IO.Path.Combine(
            directory,
            $".timereport-backup-{Guid.NewGuid():N}.db");

        try
        {
            ExportDatabase(Connection, temporary, EncryptionKey);

            if (validate)
            {
                ValidateFile(temporary, EncryptionKey);
            }
            File.Move(temporary, fullDestination, true);
            Services.AppPaths.SecureFile(fullDestination);
            return fullDestination;
        }
        catch (Exception exception)
        {
            TryDelete(temporary);
            if (exception is DatabaseException)
            {
                throw;
            }
            throw new DatabaseException($"Could not create the backup: {exception.Message}", exception);
        }
    }

    public static int ValidateFile(string path, string? encryptionKey = null)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new RestoreException("The selected file does not exist.");
        }

        try
        {
            var plaintext = IsPlaintextDatabase(fullPath);
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = fullPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false,
                }.ToString());
            connection.Open();
            if (string.IsNullOrWhiteSpace(encryptionKey) && !plaintext)
            {
                throw new RestoreException(
                    "The selected database is encrypted. The database encryption key is required.");
            }
            if (!string.IsNullOrWhiteSpace(encryptionKey) && !plaintext)
            {
                ApplyKey(connection, encryptionKey);
            }
            Scalar(connection, "PRAGMA trusted_schema = OFF", null);

            var integrity = Scalar(connection, "PRAGMA integrity_check", null) as string;
            if (integrity != "ok")
            {
                throw new RestoreException(
                    $"The selected database failed its integrity check: {integrity ?? "unknown error"}");
            }

            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            var version = Convert.ToInt32(
                Scalar(connection, "PRAGMA user_version", null),
                System.Globalization.CultureInfo.InvariantCulture);
            if (version < 1)
            {
                throw new RestoreException("The selected file is not a supported TimeReport database.");
            }
            if (version > SchemaVersion)
            {
                throw new RestoreException(
                    $"The selected database uses schema version {version}, but this version of TimeReport supports up to version {SchemaVersion}.");
            }

            ValidateTables(connection, tables, RequiredTablesV1);
            if (version >= 2)
            {
                ValidateTables(connection, tables, RequiredTablesV2);
            }
            if (version >= 3)
            {
                ValidateTables(connection, tables, RequiredTablesV3);
            }
            ValidateSchemaObjects(connection, version);
            ValidateData(connection, version);

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_key_check";
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    throw new RestoreException("The selected database has broken references.");
                }
            }

            return version;
        }
        catch (SqliteException exception)
        {
            throw new RestoreException($"The selected file could not be read: {exception.Message}", exception);
        }
    }

    public string RestoreFrom(string source)
    {
        var fullSource = System.IO.Path.GetFullPath(source);
        if (!File.Exists(fullSource))
        {
            throw new RestoreException("The selected file does not exist.");
        }
        if (Services.PathSafety.IsDatabaseFile(fullSource, Path) ||
            Services.PathSafety.AreSameFile(fullSource, Path))
        {
            throw new RestoreException("The selected file is already the live database.");
        }

        ValidateFile(fullSource, EncryptionKey);
        var sourceIsPlaintext = IsPlaintextDatabase(fullSource);
        var directory = System.IO.Path.GetDirectoryName(Path)
            ?? throw new RestoreException("The database path has no directory.");
        Services.AppPaths.EnsureDirectory(directory);
        var temporary = System.IO.Path.Combine(
            directory,
            $".timereport-restore-{Guid.NewGuid():N}.db");

        try
        {
            using (var sourceConnection = new SqliteConnection(
                       new SqliteConnectionStringBuilder
                       {
                           DataSource = fullSource,
                           Mode = SqliteOpenMode.ReadWriteCreate,
                           Pooling = false,
                       }.ToString()))
            {
                sourceConnection.Open();
                if (!string.IsNullOrWhiteSpace(EncryptionKey) && !sourceIsPlaintext)
                {
                    ApplyKey(sourceConnection, EncryptionKey);
                }
                Scalar(sourceConnection, "PRAGMA trusted_schema = OFF", null);
                ExportDatabase(sourceConnection, temporary, EncryptionKey);
            }

            using (var temporaryDatabase = new Database(temporary, EncryptionKey))
            {
                temporaryDatabase.Open();
            }
            ValidateFile(temporary, EncryptionKey);

            var autoBackup = System.IO.Path.Combine(
                Services.AppPaths.BackupsDir(directory),
                $"pre-restore-{Services.AppPaths.TimestampForFilename()}.db");
            // The live database may be exactly what is damaged. Copy it without
            // validating the copy so the user can still recover from a good backup.
            BackupToCore(autoBackup, validate: false);
            PruneBackups(directory, BackupRetentionCount);

            Close();
            foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
            {
                TryDelete(Path + suffix);
            }

            try
            {
                File.Move(temporary, Path, true);
            }
            catch
            {
                Open();
                throw;
            }

            try
            {
                Open();
            }
            catch
            {
                Close();
                foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
                {
                    TryDelete(Path + suffix);
                }
                File.Copy(autoBackup, Path, true);
                Open();
                throw;
            }

            return autoBackup;
        }
        catch (Exception exception)
        {
            TryDelete(temporary);
            if (exception is DatabaseException)
            {
                throw;
            }
            throw new RestoreException($"Could not restore the database: {exception.Message}", exception);
        }
    }

    private void Migrate()
    {
        var version = Convert.ToInt32(
            Scalar("PRAGMA user_version", null),
            System.Globalization.CultureInfo.InvariantCulture);
        if (version > SchemaVersion)
        {
            throw new DatabaseException(
                $"The database uses schema version {version}, but this version of TimeReport supports up to version {SchemaVersion}.");
        }
        if (version == SchemaVersion)
        {
            ValidateOpenSchema();
            return;
        }

        if (version > 0)
        {
            ValidateSchemaObjects(Connection, version);
            var directory = System.IO.Path.GetDirectoryName(Path)!;
            // Keep a raw copy even when the old database has data problems.
            BackupToCore(
                System.IO.Path.Combine(
                    Services.AppPaths.BackupsDir(directory),
                    $"pre-migration-v{version}-{Services.AppPaths.TimestampForFilename()}.db"),
                validate: false);
            PruneBackups(directory, BackupRetentionCount);
        }

        for (var target = version + 1; target <= SchemaVersion; target++)
        {
            if (!Migrations.TryGetValue(target, out var statements))
            {
                throw new DatabaseException($"Missing migration for schema version {target}.");
            }
            using var transaction = BeginTransaction();
            foreach (var statement in statements)
            {
                if (target == 2 &&
                    statement.Contains("ALTER TABLE entries ADD COLUMN entry_type_id", StringComparison.Ordinal) &&
                    HasColumn("entries", "entry_type_id"))
                {
                    continue;
                }
                if (target == 3 &&
                    statement.Contains("ALTER TABLE entries ADD COLUMN timezone_id", StringComparison.Ordinal) &&
                    HasColumn("entries", "timezone_id"))
                {
                    continue;
                }
                Execute(statement, transaction);
            }
            Execute($"PRAGMA user_version = {target}", transaction);
            transaction.Commit();
        }

        ValidateOpenSchema();
    }

    private void ValidateOpenSchema()
    {
        var tables = GetTables(Connection);
        try
        {
            ValidateSchemaObjects(Connection, SchemaVersionNumber);
            ValidateTables(Connection, tables, RequiredTablesV1);
            if (SchemaVersionNumber >= 2)
            {
                ValidateTables(Connection, tables, RequiredTablesV2);
            }
            if (SchemaVersionNumber >= 3)
            {
                ValidateTables(Connection, tables, RequiredTablesV3);
            }
        }
        catch (RestoreException exception)
        {
            throw new DatabaseException(exception.Message, exception);
        }
    }

    private static HashSet<string> GetTables(SqliteConnection connection)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }

    private bool HasColumn(string table, string column)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table})";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static void ValidateData(SqliteConnection connection, int version)
    {
        RejectRows(
            connection,
            "SELECT COUNT(*) FROM entries " +
            "WHERE entry_date NOT GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]' " +
            "OR date(entry_date) IS NULL OR date(entry_date) <> entry_date",
            "invalid entry dates");

        RejectRows(
            connection,
            "SELECT COUNT(*) FROM entries WHERE kind NOT IN ('work', 'lunch', 'away', 'xpause')",
            "invalid entry kinds");

        RejectRows(
            connection,
            "SELECT COUNT(*) FROM entries " +
            "WHERE start_minute < 0 OR start_minute >= 1440 " +
            "OR end_minute <= start_minute OR end_minute > 1440",
            "invalid entry times");

        RejectRows(
            connection,
            """
            SELECT COUNT(*) FROM entries
            WHERE (kind = 'work' AND (project_id IS NULL OR label IS NOT NULL))
               OR (kind = 'away' AND (project_id IS NOT NULL OR label IS NULL OR length(trim(label)) = 0))
               OR (kind IN ('lunch', 'xpause') AND (project_id IS NOT NULL OR label IS NOT NULL))
            """,
            "invalid entry project or reason data");

        if (version >= 2)
        {
            RejectRows(
                connection,
                "SELECT COUNT(*) FROM entry_types WHERE category NOT IN ('work', 'lunch', 'away', 'xpause')",
                "invalid entry type categories");

            RejectRows(
                connection,
                "SELECT COUNT(*) FROM entries WHERE entry_type_id IS NULL",
                "entries without an entry type");

            RejectRows(
                connection,
                """
                SELECT COUNT(*) FROM entries AS e
                JOIN entry_types AS t ON t.id = e.entry_type_id
                WHERE t.category <> e.kind
                """,
                "entries whose type category does not match the entry kind");
        }
    }

    private static void RejectRows(SqliteConnection connection, string sql, string description)
    {
        var count = Convert.ToInt32(
            Scalar(connection, sql, null),
            System.Globalization.CultureInfo.InvariantCulture);
        if (count > 0)
        {
            throw new RestoreException(
                $"The selected database contains {count} row(s) with {description}.");
        }
    }

    private void Execute(string sql, SqliteTransaction? transaction = null)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        command.ExecuteNonQuery();
    }

    private object? Scalar(string sql, SqliteTransaction? transaction)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command.ExecuteScalar();
    }

    private static object? Scalar(SqliteConnection connection, string sql, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command.ExecuteScalar();
    }

    private static void ValidateSchemaObjects(SqliteConnection connection, int version)
    {
        var allowedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "projects",
            "entries",
        };
        if (version >= 2)
        {
            allowedTables.Add("entry_types");
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT type, name FROM sqlite_master WHERE type IN ('table', 'view', 'trigger')";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var type = reader.GetString(0);
            var name = reader.GetString(1);
            if (string.Equals(type, "table", StringComparison.OrdinalIgnoreCase) &&
                (allowedTables.Contains(name) ||
                 name.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            throw new RestoreException(
                $"The selected database contains an unexpected {type}: {name}.");
        }
    }

    private static void ValidateTables(
        SqliteConnection connection,
        HashSet<string> tables,
        Dictionary<string, string[]> requiredTables)
    {
        var missingTables = requiredTables.Keys.Where(table => !tables.Contains(table)).ToList();
        if (missingTables.Count > 0)
        {
            throw new RestoreException(
                $"The selected file is not a TimeReport database (missing: {string.Join(", ", missingTables)}).");
        }

        foreach (var (table, requiredColumns) in requiredTables)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({table})";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }

            var missingColumns = requiredColumns.Where(column => !columns.Contains(column)).ToList();
            if (missingColumns.Count > 0)
            {
                throw new RestoreException(
                    $"The selected database is missing columns in {table}: {string.Join(", ", missingColumns)}.");
            }
        }
    }

    public string IntegrityCheck() =>
        Convert.ToString(Scalar("PRAGMA integrity_check", null), System.Globalization.CultureInfo.InvariantCulture)
        ?? "unknown";

    public void Compact()
    {
        Execute("PRAGMA optimize");
        Execute("VACUUM");
    }

    public string Repair()
    {
        var directory = System.IO.Path.GetDirectoryName(Path)
            ?? throw new DatabaseException("The database path has no directory.");
        var backup = System.IO.Path.Combine(
            Services.AppPaths.BackupsDir(directory),
            $"pre-repair-{Services.AppPaths.TimestampForFilename()}.db");
        BackupToCore(backup, validate: false);
        PruneBackups(directory, BackupRetentionCount);
        Execute("REINDEX");
        Execute("PRAGMA optimize");
        Execute("VACUUM");
        var integrity = IntegrityCheck();
        if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new DatabaseException($"The database still reports: {integrity}");
        }
        return backup;
    }

    public static int PruneBackups(string dataDirectory, int keep)
    {
        var directory = Services.AppPaths.BackupsDir(dataDirectory);
        if (!Directory.Exists(directory) || keep < 1)
        {
            return 0;
        }

        var candidates = Directory.EnumerateFiles(directory, "*.db")
            .Where(path =>
                System.IO.Path.GetFileName(path).StartsWith("pre-restore-", StringComparison.OrdinalIgnoreCase) ||
                System.IO.Path.GetFileName(path).StartsWith("pre-migration-", StringComparison.OrdinalIgnoreCase) ||
                System.IO.Path.GetFileName(path).StartsWith("pre-encryption-", StringComparison.OrdinalIgnoreCase) ||
                System.IO.Path.GetFileName(path).StartsWith("pre-repair-", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenByDescending(path => path, StringComparer.Ordinal)
            .ToList();
        var removed = 0;
        foreach (var path in candidates.Skip(keep))
        {
            TryDelete(path);
            if (!File.Exists(path))
            {
                removed++;
            }
        }
        return removed;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
