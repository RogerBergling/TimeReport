using Microsoft.Data.Sqlite;

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
    public const int SchemaVersion = 1;

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

    private static readonly Dictionary<string, string[]> RequiredTables = new()
    {
        ["projects"] = ["id", "name", "archived", "created_at"],
        ["entries"] =
        [
            "id", "entry_date", "kind", "project_id", "label",
            "start_minute", "end_minute", "created_at", "updated_at",
        ],
    };

    private SqliteConnection? _connection;

    public Database(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
    }

    public string Path { get; }

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
        Services.AppPaths.EnsureDataDir(directory);

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
            Execute("PRAGMA foreign_keys = ON");
            Execute("PRAGMA busy_timeout = 5000");
            Migrate();
        }
        catch
        {
            Close();
            throw;
        }

        Services.AppPaths.SecureFile(Path);
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

    public string BackupTo(string destination)
    {
        var fullDestination = System.IO.Path.GetFullPath(destination);
        if (PathsEqual(fullDestination, Path))
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
            using (var destinationConnection = new SqliteConnection(
                       new SqliteConnectionStringBuilder
                       {
                           DataSource = temporary,
                           Mode = SqliteOpenMode.ReadWriteCreate,
                           Pooling = false,
                       }.ToString()))
            {
                destinationConnection.Open();
                Connection.BackupDatabase(destinationConnection);
            }

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

    public static int ValidateFile(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new RestoreException("The selected file does not exist.");
        }

        try
        {
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = fullPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false,
                }.ToString());
            connection.Open();

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

            var missingTables = RequiredTables.Keys.Where(table => !tables.Contains(table)).ToList();
            if (missingTables.Count > 0)
            {
                throw new RestoreException(
                    $"The selected file is not a TimeReport database (missing: {string.Join(", ", missingTables)}).");
            }

            foreach (var (table, requiredColumns) in RequiredTables)
            {
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"PRAGMA table_info({table})";
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        columns.Add(reader.GetString(1));
                    }
                }

                var missingColumns = requiredColumns.Where(column => !columns.Contains(column)).ToList();
                if (missingColumns.Count > 0)
                {
                    throw new RestoreException(
                        $"The selected database is missing columns in {table}: {string.Join(", ", missingColumns)}.");
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_key_check";
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    throw new RestoreException("The selected database has broken project references.");
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
        if (PathsEqual(fullSource, Path))
        {
            throw new RestoreException("The selected file is already the live database.");
        }

        ValidateFile(fullSource);
        var directory = System.IO.Path.GetDirectoryName(Path)
            ?? throw new RestoreException("The database path has no directory.");
        Services.AppPaths.EnsureDataDir(directory);
        var temporary = System.IO.Path.Combine(
            directory,
            $".timereport-restore-{Guid.NewGuid():N}.db");

        try
        {
            using (var sourceConnection = new SqliteConnection(
                       new SqliteConnectionStringBuilder
                       {
                           DataSource = fullSource,
                           Mode = SqliteOpenMode.ReadOnly,
                           Pooling = false,
                       }.ToString()))
            {
                sourceConnection.Open();
                using var destinationConnection = new SqliteConnection(
                    new SqliteConnectionStringBuilder
                    {
                        DataSource = temporary,
                        Mode = SqliteOpenMode.ReadWriteCreate,
                        Pooling = false,
                    }.ToString());
                destinationConnection.Open();
                sourceConnection.BackupDatabase(destinationConnection);
            }

            using (var temporaryDatabase = new Database(temporary))
            {
                temporaryDatabase.Open();
            }
            ValidateFile(temporary);

            var autoBackup = System.IO.Path.Combine(
                Services.AppPaths.BackupsDir(directory),
                $"pre-restore-{Services.AppPaths.TimestampForFilename()}.db");
            BackupTo(autoBackup);

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
            return;
        }

        if (version > 0)
        {
            var directory = System.IO.Path.GetDirectoryName(Path)!;
            BackupTo(System.IO.Path.Combine(
                Services.AppPaths.BackupsDir(directory),
                $"pre-migration-v{version}-{Services.AppPaths.TimestampForFilename()}.db"));
        }

        for (var target = version + 1; target <= SchemaVersion; target++)
        {
            using var transaction = BeginTransaction();
            foreach (var statement in SchemaV1)
            {
                Execute(statement, transaction);
            }
            Execute($"PRAGMA user_version = {target}", transaction);
            transaction.Commit();
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

    private static bool PathsEqual(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(
            System.IO.Path.GetFullPath(first),
            System.IO.Path.GetFullPath(second),
            comparison);
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
