"""SQLite persistence, schema migrations, backup and restore."""

from __future__ import annotations

import os
import shutil
import sqlite3
import tempfile
from collections.abc import Iterator
from contextlib import contextmanager
from datetime import datetime
from pathlib import Path

from .paths import backups_dir, ensure_data_dir

SCHEMA_VERSION = 1


class DatabaseError(RuntimeError):
    """Base error for database operations."""


class RestoreError(DatabaseError):
    """Raised when a selected restore file is not usable."""


SCHEMA_V1_STATEMENTS: tuple[str, ...] = (
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
)

MIGRATIONS: dict[int, tuple[str, ...]] = {1: SCHEMA_V1_STATEMENTS}

REQUIRED_TABLES: dict[str, set[str]] = {
    "projects": {"id", "name", "archived", "created_at"},
    "entries": {
        "id",
        "entry_date",
        "kind",
        "project_id",
        "label",
        "start_minute",
        "end_minute",
        "created_at",
        "updated_at",
    },
}


def _timestamp_for_filename() -> str:
    return datetime.now().strftime("%Y%m%d-%H%M%S-%f")


def _same_path(first: Path, second: Path) -> bool:
    return os.path.normcase(str(first.resolve())) == os.path.normcase(str(second.resolve()))


class Database:
    def __init__(self, path: Path):
        self.path = Path(path)
        self._connection: sqlite3.Connection | None = None

    @property
    def conn(self) -> sqlite3.Connection:
        if self._connection is None:
            raise DatabaseError("The database is not open.")
        return self._connection

    @property
    def schema_version(self) -> int:
        return int(self.conn.execute("PRAGMA user_version").fetchone()[0])

    def connect(self) -> None:
        if self._connection is not None:
            return

        ensure_data_dir(self.path.parent)
        connection = sqlite3.connect(self.path, timeout=5)
        connection.row_factory = sqlite3.Row
        connection.execute("PRAGMA foreign_keys = ON")
        connection.execute("PRAGMA busy_timeout = 5000")
        self._connection = connection
        try:
            self._migrate()
        except Exception:
            self.close()
            raise
        self._secure_file()

    def close(self) -> None:
        if self._connection is not None:
            self._connection.close()
            self._connection = None

    def _secure_file(self) -> None:
        if os.name != "nt":
            try:
                self.path.chmod(0o600)
            except OSError:
                pass

    @contextmanager
    def transaction(self) -> Iterator[sqlite3.Connection]:
        connection = self.conn
        try:
            yield connection
            connection.commit()
        except Exception:
            connection.rollback()
            raise

    def _migrate(self) -> None:
        version = int(self.conn.execute("PRAGMA user_version").fetchone()[0])
        if version > SCHEMA_VERSION:
            raise DatabaseError(
                f"The database uses schema version {version}, but this version of "
                f"TimeReport supports up to version {SCHEMA_VERSION}."
            )

        if version == SCHEMA_VERSION:
            return

        if version > 0:
            backup_path = backups_dir(self.path.parent) / (
                f"pre-migration-v{version}-{_timestamp_for_filename()}.db"
            )
            self.backup_to(backup_path)

        for target_version in range(version + 1, SCHEMA_VERSION + 1):
            statements = MIGRATIONS.get(target_version)
            if statements is None:
                raise DatabaseError(f"Missing migration for schema version {target_version}.")
            with self.transaction() as connection:
                for statement in statements:
                    connection.execute(statement)
                connection.execute(f"PRAGMA user_version = {target_version}")

    def backup_to(self, destination: Path) -> Path:
        """Create a consistent backup at ``destination`` using SQLite's backup API."""

        destination = Path(destination).expanduser()
        if _same_path(destination, self.path):
            raise DatabaseError("Choose a backup location other than the live database.")

        destination.parent.mkdir(parents=True, exist_ok=True)
        file_descriptor, temporary_name = tempfile.mkstemp(
            prefix=".timereport-backup-", suffix=".db", dir=destination.parent
        )
        os.close(file_descriptor)
        temporary_path = Path(temporary_name)

        try:
            self.conn.commit()
            backup_connection = sqlite3.connect(temporary_path)
            try:
                self.conn.backup(backup_connection)
            finally:
                backup_connection.close()
            self.validate_file(temporary_path)
            os.replace(temporary_path, destination)
            if os.name != "nt":
                destination.chmod(0o600)
            return destination
        except Exception as exc:
            temporary_path.unlink(missing_ok=True)
            if isinstance(exc, DatabaseError):
                raise
            raise DatabaseError(f"Could not create the backup: {exc}") from exc

    @staticmethod
    def validate_file(path: Path) -> int:
        """Validate a SQLite file and return its supported schema version."""

        path = Path(path).expanduser()
        if not path.is_file():
            raise RestoreError("The selected file does not exist.")

        try:
            connection = sqlite3.connect(f"{path.resolve().as_uri()}?mode=ro", uri=True)
        except sqlite3.Error as exc:
            raise RestoreError(
                f"The selected file is not a readable SQLite database: {exc}"
            ) from exc

        try:
            integrity = connection.execute("PRAGMA integrity_check").fetchone()
            if not integrity or integrity[0] != "ok":
                detail = integrity[0] if integrity else "unknown integrity error"
                raise RestoreError(f"The selected database failed its integrity check: {detail}")

            tables = {
                row[0]
                for row in connection.execute(
                    "SELECT name FROM sqlite_master WHERE type = 'table'"
                ).fetchall()
            }
            missing_tables = set(REQUIRED_TABLES) - tables
            if missing_tables:
                raise RestoreError(
                    "The selected file is not a TimeReport database "
                    f"(missing: {', '.join(sorted(missing_tables))})."
                )

            for table, required_columns in REQUIRED_TABLES.items():
                columns = {
                    row[1] for row in connection.execute(f"PRAGMA table_info({table})").fetchall()
                }
                missing_columns = required_columns - columns
                if missing_columns:
                    raise RestoreError(
                        f"The selected database is missing columns in {table}: "
                        f"{', '.join(sorted(missing_columns))}."
                    )

            foreign_key_errors = connection.execute("PRAGMA foreign_key_check").fetchall()
            if foreign_key_errors:
                raise RestoreError("The selected database has broken project references.")

            version = int(connection.execute("PRAGMA user_version").fetchone()[0])
            if version < 1:
                raise RestoreError("The selected file is not a supported TimeReport database.")
            if version > SCHEMA_VERSION:
                raise RestoreError(
                    f"The selected database uses schema version {version}, but this version "
                    f"of TimeReport supports up to version {SCHEMA_VERSION}."
                )
            return version
        except sqlite3.Error as exc:
            raise RestoreError(f"The selected file could not be read: {exc}") from exc
        finally:
            connection.close()

    def restore_from(self, source: Path) -> Path:
        """Replace the live database with a validated backup.

        The current database is backed up first. Returns the automatic backup
        path so the caller can tell the user where the previous data went.
        """

        source = Path(source).expanduser()
        if not source.is_file():
            raise RestoreError("The selected file does not exist.")
        if _same_path(source, self.path):
            raise RestoreError("The selected file is already the live database.")

        self.validate_file(source)
        ensure_data_dir(self.path.parent)

        file_descriptor, temporary_name = tempfile.mkstemp(
            prefix=".timereport-restore-", suffix=".db", dir=self.path.parent
        )
        os.close(file_descriptor)
        temporary_path = Path(temporary_name)

        try:
            source_connection = sqlite3.connect(f"{source.resolve().as_uri()}?mode=ro", uri=True)
            try:
                destination_connection = sqlite3.connect(temporary_path)
                try:
                    source_connection.backup(destination_connection)
                finally:
                    destination_connection.close()
            finally:
                source_connection.close()

            temporary_database = Database(temporary_path)
            temporary_database.connect()
            temporary_database.close()
            self.validate_file(temporary_path)

            auto_backup = backups_dir(self.path.parent) / (
                f"pre-restore-{_timestamp_for_filename()}.db"
            )
            self.backup_to(auto_backup)

            self.close()
            for suffix in ("-wal", "-shm", "-journal"):
                self.path.with_name(self.path.name + suffix).unlink(missing_ok=True)

            try:
                os.replace(temporary_path, self.path)
            except Exception:
                self.connect()
                raise

            try:
                self.connect()
            except Exception:
                self.close()
                for suffix in ("-wal", "-shm", "-journal"):
                    self.path.with_name(self.path.name + suffix).unlink(missing_ok=True)
                shutil.copy2(auto_backup, self.path)
                self.connect()
                raise

            return auto_backup
        except Exception as exc:
            temporary_path.unlink(missing_ok=True)
            if isinstance(exc, DatabaseError):
                raise
            raise RestoreError(f"Could not restore the database: {exc}") from exc
