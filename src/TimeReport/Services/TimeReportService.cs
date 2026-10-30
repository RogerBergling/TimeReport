using System.Globalization;
using Microsoft.Data.Sqlite;
using TimeReport.Data;
using TimeReport.Models;

namespace TimeReport.Services;

public sealed class TimeReportService
{
    public TimeReportService(Database database)
    {
        Database = database;
    }

    public Database Database { get; }

    // Projects -----------------------------------------------------------------

    public IReadOnlyList<Project> ListProjects(bool includeArchived = false)
    {
        var sql = includeArchived
            ? "SELECT id, name, archived FROM projects ORDER BY name COLLATE NOCASE"
            : "SELECT id, name, archived FROM projects WHERE archived = 0 ORDER BY name COLLATE NOCASE";
        using var command = Database.Connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var projects = new List<Project>();
        while (reader.Read())
        {
            projects.Add(new Project(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt64(2) != 0));
        }
        return projects;
    }

    public Project? GetProject(long projectId)
    {
        using var command = Database.Connection.CreateCommand();
        command.CommandText = "SELECT id, name, archived FROM projects WHERE id = $id";
        command.Parameters.AddWithValue("$id", projectId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }
        return new Project(reader.GetInt64(0), reader.GetString(1), reader.GetInt64(2) != 0);
    }

    public Project CreateProject(string name)
    {
        var cleaned = CleanProjectName(name);
        using var transaction = Database.BeginTransaction();
        try
        {
            using var command = Database.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO projects (name) VALUES ($name) RETURNING id";
            command.Parameters.AddWithValue("$name", cleaned);
            var id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            transaction.Commit();
            return new Project(id, cleaned);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new ValidationException(LocalizationService.T("ValidationProjectExists", cleaned));
        }
    }

    public Project RenameProject(long projectId, string name)
    {
        var cleaned = CleanProjectName(name);
        using var transaction = Database.BeginTransaction();
        try
        {
            using var command = Database.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE projects SET name = $name WHERE id = $id";
            command.Parameters.AddWithValue("$name", cleaned);
            command.Parameters.AddWithValue("$id", projectId);
            if (command.ExecuteNonQuery() == 0)
            {
                throw new ValidationException(LocalizationService.T("ValidationProjectNotFound"));
            }

            using var archivedCommand = Database.Connection.CreateCommand();
            archivedCommand.Transaction = transaction;
            archivedCommand.CommandText = "SELECT archived FROM projects WHERE id = $id";
            archivedCommand.Parameters.AddWithValue("$id", projectId);
            var archived = Convert.ToInt32(archivedCommand.ExecuteScalar(), CultureInfo.InvariantCulture) != 0;
            transaction.Commit();
            return new Project(projectId, cleaned, archived);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new ValidationException(LocalizationService.T("ValidationProjectExists", cleaned));
        }
    }

    public void SetProjectArchived(long projectId, bool archived)
    {
        using var command = Database.Connection.CreateCommand();
        command.CommandText = "UPDATE projects SET archived = $archived WHERE id = $id";
        command.Parameters.AddWithValue("$archived", archived ? 1 : 0);
        command.Parameters.AddWithValue("$id", projectId);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new ValidationException(LocalizationService.T("ValidationProjectNotFound"));
        }
    }

    public void DeleteProject(long projectId)
    {
        if (ProjectEntryCount(projectId) > 0)
        {
            throw new ValidationException(LocalizationService.T("ValidationProjectUsed"));
        }

        using var command = Database.Connection.CreateCommand();
        command.CommandText = "DELETE FROM projects WHERE id = $id";
        command.Parameters.AddWithValue("$id", projectId);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new ValidationException(LocalizationService.T("ValidationProjectNotFound"));
        }
    }

    public int ProjectEntryCount(long projectId)
    {
        using var command = Database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM entries WHERE project_id = $id";
        command.Parameters.AddWithValue("$id", projectId);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string CleanProjectName(string name)
    {
        var cleaned = string.Join(" ", (name ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (cleaned.Length == 0)
        {
            throw new ValidationException(LocalizationService.T("ValidationProjectNameEmpty"));
        }
        if (cleaned.Length > 80)
        {
            throw new ValidationException(LocalizationService.T("ValidationProjectNameLength"));
        }
        return cleaned;
    }

    // Entry types --------------------------------------------------------------

    public IReadOnlyList<EntryType> ListEntryTypes(bool includeArchived = false)
    {
        var sql = includeArchived
            ? "SELECT id, name, category, archived FROM entry_types ORDER BY name COLLATE NOCASE"
            : "SELECT id, name, category, archived FROM entry_types WHERE archived = 0 ORDER BY name COLLATE NOCASE";
        using var command = Database.Connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var types = new List<EntryType>();
        while (reader.Read())
        {
            types.Add(RowToEntryType(reader));
        }
        return types;
    }

    public EntryType? GetEntryType(long typeId)
    {
        using var command = Database.Connection.CreateCommand();
        command.CommandText = "SELECT id, name, category, archived FROM entry_types WHERE id = $id";
        command.Parameters.AddWithValue("$id", typeId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? RowToEntryType(reader) : null;
    }

    public EntryType CreateEntryType(string name, EntryKind category)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ValidationException(LocalizationService.T("ValidationTypeCategory"));
        }

        var cleaned = CleanEntryTypeName(name);
        using var transaction = Database.BeginTransaction();
        try
        {
            using var command = Database.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO entry_types (name, category)
                VALUES ($name, $category)
                RETURNING id
                """;
            command.Parameters.AddWithValue("$name", cleaned);
            command.Parameters.AddWithValue("$category", category.ToDbString());
            var id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            transaction.Commit();
            return new EntryType(id, cleaned, category);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new ValidationException(LocalizationService.T("ValidationTypeExists", cleaned));
        }
    }

    public EntryType RenameEntryType(long typeId, string name)
    {
        var cleaned = CleanEntryTypeName(name);
        using var transaction = Database.BeginTransaction();
        try
        {
            using var command = Database.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE entry_types SET name = $name WHERE id = $id";
            command.Parameters.AddWithValue("$name", cleaned);
            command.Parameters.AddWithValue("$id", typeId);
            if (command.ExecuteNonQuery() == 0)
            {
                throw new ValidationException(LocalizationService.T("ValidationTypeNotFound"));
            }

            using var typeCommand = Database.Connection.CreateCommand();
            typeCommand.Transaction = transaction;
            typeCommand.CommandText = "SELECT category, archived FROM entry_types WHERE id = $id";
            typeCommand.Parameters.AddWithValue("$id", typeId);
            using var reader = typeCommand.ExecuteReader();
            if (!reader.Read())
            {
                throw new ValidationException(LocalizationService.T("ValidationTypeNotFound"));
            }
            var category = EntryKindExtensions.FromDbString(reader.GetString(0));
            var archived = reader.GetInt64(1) != 0;
            transaction.Commit();
            return new EntryType(typeId, cleaned, category, archived);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new ValidationException(LocalizationService.T("ValidationTypeExists", cleaned));
        }
    }

    public void SetEntryTypeArchived(long typeId, bool archived)
    {
        using var command = Database.Connection.CreateCommand();
        command.CommandText = "UPDATE entry_types SET archived = $archived WHERE id = $id";
        command.Parameters.AddWithValue("$archived", archived ? 1 : 0);
        command.Parameters.AddWithValue("$id", typeId);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new ValidationException(LocalizationService.T("ValidationTypeNotFound"));
        }
    }

    public void DeleteEntryType(long typeId)
    {
        if (EntryTypeUsageCount(typeId) > 0)
        {
            throw new ValidationException(LocalizationService.T("ValidationTypeUsed"));
        }

        using var command = Database.Connection.CreateCommand();
        command.CommandText = "DELETE FROM entry_types WHERE id = $id";
        command.Parameters.AddWithValue("$id", typeId);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new ValidationException(LocalizationService.T("ValidationTypeNotFound"));
        }
    }

    public int EntryTypeUsageCount(long typeId)
    {
        using var command = Database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM entries WHERE entry_type_id = $id";
        command.Parameters.AddWithValue("$id", typeId);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string CleanEntryTypeName(string name)
    {
        var cleaned = string.Join(" ", (name ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (cleaned.Length == 0)
        {
            throw new ValidationException(LocalizationService.T("ValidationTypeNameEmpty"));
        }
        if (cleaned.Length > 80)
        {
            throw new ValidationException(LocalizationService.T("ValidationTypeNameLength"));
        }
        return cleaned;
    }

    private static EntryType RowToEntryType(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        EntryKindExtensions.FromDbString(reader.GetString(2)),
        reader.GetInt64(3) != 0);

    // Entries ------------------------------------------------------------------

    public IReadOnlyList<Entry> ListEntries(DateOnly start, DateOnly end) =>
        FetchEntries(Database.Connection, null, start, end);

    public Entry? GetEntry(long entryId) => GetEntry(Database.Connection, null, entryId);

    public Entry SaveEntry(Entry entry)
    {
        var original = entry.Id is long originalId ? GetEntry(originalId) : null;
        if (entry.Id is not null && original is null)
        {
            throw new ValidationException(LocalizationService.T("ValidationEntryNotFound"));
        }

        var entryType = ResolveEntryType(entry, original);
        entry = NormalizeEntry(entry, entryType);

        var firstDay = TimeUtil.WeekStart(entry.Date);
        var lastDay = firstDay.AddDays(6);
        long savedId;

        using (var transaction = Database.BeginTransaction())
        {
            try
            {
                if (original is not null)
                {
                    var oldFirstDay = TimeUtil.WeekStart(original.Date);
                    if (oldFirstDay != firstDay)
                    {
                        var oldEntries = FetchEntries(
                            Database.Connection,
                            transaction,
                            oldFirstDay,
                            oldFirstDay.AddDays(6));
                        ValidateWeekXPause(oldEntries.Where(item => item.Id != entry.Id).ToList());
                    }
                }

                var weekEntries = FetchEntries(Database.Connection, transaction, firstDay, lastDay);
                var existing = weekEntries.Where(item => item.Id != entry.Id).ToList();
                ValidateCandidate(entry, existing);
                ValidateWeekXPause([.. existing, entry]);
                ValidateProject(entry, original, transaction);

                var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                if (entry.Id is null)
                {
                    using var command = Database.Connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = """
                        INSERT INTO entries (
                            entry_date, kind, project_id, label, entry_type_id,
                            start_minute, end_minute, created_at, updated_at
                        ) VALUES (
                            $date, $kind, $projectId, $label, $typeId,
                            $start, $end, $created, $updated
                        )
                        RETURNING id
                        """;
                    command.Parameters.AddWithValue("$date", FormatDbDate(entry.Date));
                    command.Parameters.AddWithValue("$kind", entry.Kind.ToDbString());
                    command.Parameters.AddWithValue("$projectId", (object?)entry.ProjectId ?? DBNull.Value);
                    command.Parameters.AddWithValue("$label", (object?)entry.AwayReason ?? DBNull.Value);
                    command.Parameters.AddWithValue("$typeId", (object?)entry.TypeId ?? DBNull.Value);
                    command.Parameters.AddWithValue("$start", entry.StartMinute);
                    command.Parameters.AddWithValue("$end", entry.EndMinute);
                    command.Parameters.AddWithValue("$created", now);
                    command.Parameters.AddWithValue("$updated", now);
                    savedId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                }
                else
                {
                    using var command = Database.Connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = """
                        UPDATE entries SET
                            entry_date = $date,
                            kind = $kind,
                            project_id = $projectId,
                            label = $label,
                            entry_type_id = $typeId,
                            start_minute = $start,
                            end_minute = $end,
                            updated_at = $updated
                        WHERE id = $id
                        """;
                    command.Parameters.AddWithValue("$date", FormatDbDate(entry.Date));
                    command.Parameters.AddWithValue("$kind", entry.Kind.ToDbString());
                    command.Parameters.AddWithValue("$projectId", (object?)entry.ProjectId ?? DBNull.Value);
                    command.Parameters.AddWithValue("$label", (object?)entry.AwayReason ?? DBNull.Value);
                    command.Parameters.AddWithValue("$typeId", (object?)entry.TypeId ?? DBNull.Value);
                    command.Parameters.AddWithValue("$start", entry.StartMinute);
                    command.Parameters.AddWithValue("$end", entry.EndMinute);
                    command.Parameters.AddWithValue("$updated", now);
                    command.Parameters.AddWithValue("$id", entry.Id.Value);
                    if (command.ExecuteNonQuery() == 0)
                    {
                        throw new ValidationException(LocalizationService.T("ValidationEntryNotFound"));
                    }
                    savedId = entry.Id.Value;
                }

                transaction.Commit();
            }
            catch
            {
                RollbackQuietly(transaction);
                throw;
            }
        }

        return GetEntry(savedId) ?? throw new DatabaseException("The saved entry could not be read back.");
    }

    public void DeleteEntry(long entryId)
    {
        using var transaction = Database.BeginTransaction();
        try
        {
            var entry = GetEntry(Database.Connection, transaction, entryId)
                ?? throw new ValidationException(LocalizationService.T("ValidationEntryNotFound"));
            var firstDay = TimeUtil.WeekStart(entry.Date);
            var weekEntries = FetchEntries(
                Database.Connection,
                transaction,
                firstDay,
                firstDay.AddDays(6));
            ValidateWeekXPause(weekEntries.Where(item => item.Id != entryId).ToList());

            using var command = Database.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM entries WHERE id = $id";
            command.Parameters.AddWithValue("$id", entryId);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch
        {
            RollbackQuietly(transaction);
            throw;
        }
    }

    private static List<Entry> FetchEntries(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        DateOnly start,
        DateOnly end)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT
                e.id, e.entry_date, e.kind, e.project_id, e.label,
                e.start_minute, e.end_minute, p.name AS project_name,
                e.entry_type_id, t.name AS type_name
            FROM entries AS e
            LEFT JOIN projects AS p ON p.id = e.project_id
            LEFT JOIN entry_types AS t ON t.id = e.entry_type_id
            WHERE e.entry_date BETWEEN $start AND $end
            ORDER BY e.entry_date, e.start_minute, e.id
            """;
        command.Parameters.AddWithValue("$start", FormatDbDate(start));
        command.Parameters.AddWithValue("$end", FormatDbDate(end));
        using var reader = command.ExecuteReader();
        var entries = new List<Entry>();
        while (reader.Read())
        {
            entries.Add(RowToEntry(reader));
        }
        return entries;
    }

    private static Entry? GetEntry(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        long entryId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT
                e.id, e.entry_date, e.kind, e.project_id, e.label,
                e.start_minute, e.end_minute, p.name AS project_name,
                e.entry_type_id, t.name AS type_name
            FROM entries AS e
            LEFT JOIN projects AS p ON p.id = e.project_id
            LEFT JOIN entry_types AS t ON t.id = e.entry_type_id
            WHERE e.id = $id
            """;
        command.Parameters.AddWithValue("$id", entryId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? RowToEntry(reader) : null;
    }

    private static Entry RowToEntry(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        DateOnly.ParseExact(reader.GetString(1), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        EntryKindExtensions.FromDbString(reader.GetString(2)),
        reader.GetInt32(5),
        reader.GetInt32(6),
        reader.IsDBNull(3) ? null : reader.GetInt64(3),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(8) ? null : reader.GetInt64(8),
        reader.IsDBNull(9) ? null : reader.GetString(9));

    private EntryType ResolveEntryType(Entry entry, Entry? original)
    {
        if (entry.TypeId is long typeId)
        {
            var type = GetEntryType(typeId)
                ?? throw new ValidationException(LocalizationService.T("ValidationTypeMissing"));
            if (type.Archived && (original is null || original.TypeId != type.Id))
            {
                throw new ValidationException(LocalizationService.T("ValidationTypeArchived"));
            }
            return type;
        }

        var types = ListEntryTypes(includeArchived: true);
        var defaultName = entry.Kind.DisplayName();
        return types.FirstOrDefault(item =>
                   item.Category == entry.Kind &&
                   !item.Archived &&
                   string.Equals(item.Name, defaultName, StringComparison.OrdinalIgnoreCase))
               ?? types.FirstOrDefault(item => item.Category == entry.Kind && !item.Archived)
               ?? types.FirstOrDefault(item => item.Category == entry.Kind)
               ?? throw new ValidationException(LocalizationService.T("ValidationTypeMissing"));
    }

    private static Entry NormalizeEntry(Entry entry, EntryType entryType)
    {
        if (entry.StartMinute < 0 || entry.EndMinute > TimeUtil.MinutesPerDay)
        {
            throw new ValidationException(LocalizationService.T("ValidationTimesRange"));
        }
        if (entry.EndMinute <= entry.StartMinute)
        {
            throw new ValidationException(LocalizationService.T("ValidationEndAfterStart"));
        }

        var kind = entryType.Category;
        var reason = string.IsNullOrWhiteSpace(entry.AwayReason) ? null : entry.AwayReason.Trim();
        var projectId = entry.ProjectId;

        switch (kind)
        {
            case EntryKind.Work:
                if (projectId is null)
                {
                    throw new ValidationException(LocalizationService.T("ValidationChooseProject"));
                }
                reason = null;
                break;
            case EntryKind.Away:
                reason ??= entryType.Name;
                if (reason.Length > 80)
                {
                    throw new ValidationException(LocalizationService.T("ValidationAwayReasonLength"));
                }
                if (projectId is not null)
                {
                    throw new ValidationException(LocalizationService.T("ValidationAwayNoProject"));
                }
                projectId = null;
                break;
            default:
                if (projectId is not null)
                {
                    throw new ValidationException(
                        LocalizationService.T("ValidationNonWorkNoProject", kind.DisplayName()));
                }
                reason = null;
                break;
        }

        return entry with
        {
            Kind = kind,
            TypeId = entryType.Id,
            TypeName = entryType.Name,
            ProjectId = projectId,
            ProjectName = null,
            AwayReason = reason,
        };
    }

    private static void ValidateCandidate(Entry candidate, List<Entry> existing)
    {
        var sameDay = existing.Where(item => item.Date == candidate.Date).ToList();
        switch (candidate.Kind)
        {
            case EntryKind.Work:
                {
                    var other = FindOverlap(candidate, sameDay, [EntryKind.Work, EntryKind.Away]);
                    if (other is not null)
                    {
                        throw new ValidationException(OverlapMessage(candidate, other));
                    }
                    break;
                }
            case EntryKind.Lunch:
                {
                    var other = FindOverlap(
                        candidate,
                        sameDay,
                        [EntryKind.Lunch, EntryKind.Away, EntryKind.XPause]);
                    if (other is not null)
                    {
                        throw new ValidationException(OverlapMessage(candidate, other));
                    }
                    break;
                }
            case EntryKind.Away:
                {
                    var other = FindOverlap(
                        candidate,
                        sameDay,
                        [EntryKind.Work, EntryKind.Lunch, EntryKind.Away, EntryKind.XPause]);
                    if (other is not null)
                    {
                        throw new ValidationException(OverlapMessage(candidate, other));
                    }
                    break;
                }
            case EntryKind.XPause:
                {
                    if (candidate.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    {
                        throw new ValidationException(LocalizationService.T("ValidationXPauseWeekday"));
                    }

                    var duration = candidate.DurationMinutes;
                    if (duration % TimeUtil.XPauseStep != 0)
                    {
                        throw new ValidationException(LocalizationService.T("ValidationXPauseStep"));
                    }
                    if (duration > TimeUtil.XPauseWeeklyLimit)
                    {
                        throw new ValidationException(LocalizationService.T("ValidationXPauseDayLimit"));
                    }

                    var used = existing
                        .Where(item => item.Kind == EntryKind.XPause &&
                                       TimeUtil.WeekStart(item.Date) == TimeUtil.WeekStart(candidate.Date))
                        .Sum(item => item.DurationMinutes);
                    var remaining = TimeUtil.XPauseWeeklyLimit - used;
                    if (duration > remaining)
                    {
                        throw new ValidationException(
                            LocalizationService.T(
                                "ValidationXPauseWeekly",
                                TimeUtil.FormatDuration(Math.Max(0, remaining))));
                    }

                    var containingWork = sameDay.Any(item =>
                        item.Kind == EntryKind.Work &&
                        TimeUtil.StrictlyInside(
                            candidate.StartMinute,
                            candidate.EndMinute,
                            item.StartMinute,
                            item.EndMinute));
                    if (!containingWork)
                    {
                        throw new ValidationException(
                            LocalizationService.T("ValidationXPauseInside"));
                    }

                    var other = FindOverlap(
                        candidate,
                        sameDay,
                        [EntryKind.Lunch, EntryKind.Away, EntryKind.XPause]);
                    if (other is not null)
                    {
                        throw new ValidationException(OverlapMessage(candidate, other));
                    }
                    break;
                }
        }
    }

    private static void ValidateWeekXPause(List<Entry> entries)
    {
        var xpauses = entries.Where(item => item.Kind == EntryKind.XPause).ToList();
        if (xpauses.Sum(item => item.DurationMinutes) > TimeUtil.XPauseWeeklyLimit)
        {
            throw new ValidationException(LocalizationService.T("ValidationXPauseWeeklySimple"));
        }

        foreach (var xpause in xpauses)
        {
            ValidateXPauseEntry(
                xpause,
                entries.Where(item => !ReferenceEquals(item, xpause)).ToList());
        }
    }

    private static void ValidateXPauseEntry(Entry xpause, List<Entry> entries)
    {
        if (xpause.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            throw new ValidationException(LocalizationService.T("ValidationXPauseWeekday"));
        }
        if (xpause.DurationMinutes % TimeUtil.XPauseStep != 0)
        {
            throw new ValidationException(LocalizationService.T("ValidationXPauseStep"));
        }
        if (xpause.DurationMinutes > TimeUtil.XPauseWeeklyLimit)
        {
            throw new ValidationException(LocalizationService.T("ValidationXPauseDayLimit"));
        }

        var sameDay = entries.Where(item => item.Date == xpause.Date).ToList();
        var containingWork = sameDay.Any(item =>
            item.Kind == EntryKind.Work &&
            TimeUtil.StrictlyInside(
                xpause.StartMinute,
                xpause.EndMinute,
                item.StartMinute,
                item.EndMinute));
        if (!containingWork)
        {
            throw new ValidationException(LocalizationService.T("ValidationXPauseOrphan", TimeUtil.FormatDateLong(xpause.Date)));
        }

        var other = FindOverlap(
            xpause,
            sameDay,
            [EntryKind.Lunch, EntryKind.Away, EntryKind.XPause]);
        if (other is not null)
        {
            throw new ValidationException(OverlapMessage(xpause, other));
        }
    }

    private void ValidateProject(
        Entry entry,
        Entry? original,
        SqliteTransaction transaction)
    {
        if (entry.Kind != EntryKind.Work)
        {
            return;
        }

        using var command = Database.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT archived FROM projects WHERE id = $id";
        command.Parameters.AddWithValue("$id", entry.ProjectId);
        var result = command.ExecuteScalar();
        if (result is null)
        {
            throw new ValidationException(LocalizationService.T("ValidationProjectMissing"));
        }
        var archived = Convert.ToInt32(result, CultureInfo.InvariantCulture) != 0;
        if (archived && (original is null || original.ProjectId != entry.ProjectId))
        {
            throw new ValidationException(LocalizationService.T("ValidationProjectArchived"));
        }
    }

    private static Entry? FindOverlap(Entry candidate, List<Entry> entries, EntryKind[] kinds)
    {
        foreach (var item in entries)
        {
            if (!kinds.Contains(item.Kind))
            {
                continue;
            }
            if (TimeUtil.IntervalsOverlap(
                    candidate.StartMinute,
                    candidate.EndMinute,
                    item.StartMinute,
                    item.EndMinute))
            {
                return item;
            }
        }
        return null;
    }

    private static string OverlapMessage(Entry candidate, Entry other)
    {
        var detail = other.ProjectName ?? other.AwayReason;
        var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" · {detail}";
        return LocalizationService.T(
            "ValidationOverlap",
            (candidate.TypeName ?? candidate.Kind.DisplayName()).ToLowerInvariant(),
            (other.TypeName ?? other.Kind.DisplayName()).ToLowerInvariant(),
            TimeUtil.FormatTime(other.StartMinute),
            TimeUtil.FormatTime(other.EndMinute),
            suffix);
    }

    // Totals -------------------------------------------------------------------

    public Totals DailyTotals(DateOnly day, IReadOnlyList<Entry>? entries = null) =>
        CalculateTotals(entries ?? ListEntries(day, day));

    public List<(DateOnly Date, Totals Totals)> TotalsByDay(
        DateOnly start,
        DateOnly end,
        bool includeEmpty = true)
    {
        var byDate = ListEntries(start, end)
            .GroupBy(entry => entry.Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        var days = includeEmpty
            ? TimeUtil.DateRangeDays(start, end).ToList()
            : byDate.Keys.OrderBy(day => day).ToList();
        return days
            .Select(day => (day, CalculateTotals(
                byDate.TryGetValue(day, out var entries) ? entries : [])))
            .ToList();
    }

    public Totals RangeTotals(DateOnly start, DateOnly end) =>
        TotalsByDay(start, end, false)
            .Aggregate(new Totals(), (total, row) => total + row.Totals);

    public List<(DateOnly Date, Totals Totals)> TotalsByMonth(int year)
    {
        var results = new List<(DateOnly Date, Totals Totals)>();
        for (var month = 1; month <= 12; month++)
        {
            var start = new DateOnly(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            results.Add((start, RangeTotals(start, end)));
        }
        return results;
    }

    public List<(DateOnly Date, Totals Totals)> TotalsByYear(int startYear, int endYear)
    {
        var results = new List<(DateOnly Date, Totals Totals)>();
        for (var year = startYear; year <= endYear; year++)
        {
            var start = new DateOnly(year, 1, 1);
            var end = new DateOnly(year, 12, 31);
            results.Add((start, RangeTotals(start, end)));
        }
        return results;
    }

    public (DateOnly Start, DateOnly End)? AllTimeBounds()
    {
        using var command = Database.Connection.CreateCommand();
        command.CommandText = "SELECT MIN(entry_date), MAX(entry_date) FROM entries";
        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0) || reader.IsDBNull(1))
        {
            return null;
        }
        return (
            DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateOnly.ParseExact(reader.GetString(1), "yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    public Totals AllTimeTotals()
    {
        var bounds = AllTimeBounds();
        return bounds is null ? new Totals() : RangeTotals(bounds.Value.Start, bounds.Value.End);
    }

    public int XPauseUsedInWeek(DateOnly firstDay, long? excludeEntryId = null) =>
        ListEntries(firstDay, firstDay.AddDays(6))
            .Where(entry => entry.Kind == EntryKind.XPause && entry.Id != excludeEntryId)
            .Sum(entry => entry.DurationMinutes);

    public int XPauseRemaining(DateOnly day, long? excludeEntryId = null) =>
        Math.Max(
            0,
            TimeUtil.XPauseWeeklyLimit -
            XPauseUsedInWeek(TimeUtil.WeekStart(day), excludeEntryId));

    private static Totals CalculateTotals(IReadOnlyList<Entry> entries)
    {
        var work = entries
            .Where(entry => entry.Kind == EntryKind.Work)
            .Select(entry => (entry.StartMinute, entry.EndMinute))
            .ToList();
        var lunch = entries
            .Where(entry => entry.Kind == EntryKind.Lunch)
            .Select(entry => (entry.StartMinute, entry.EndMinute))
            .ToList();
        var away = entries
            .Where(entry => entry.Kind == EntryKind.Away)
            .Select(entry => (entry.StartMinute, entry.EndMinute))
            .ToList();
        var xpause = entries
            .Where(entry => entry.Kind == EntryKind.XPause)
            .Select(entry => (entry.StartMinute, entry.EndMinute))
            .ToList();

        var worked = TimeUtil.TotalCovered(TimeUtil.SubtractIntervals(work, lunch));
        var lunchTotal = TimeUtil.TotalCovered(lunch);
        var awayTotal = TimeUtil.TotalCovered(away);
        var xpauseTotal = TimeUtil.TotalCovered(xpause);
        return new Totals(
            worked,
            lunchTotal,
            awayTotal,
            xpauseTotal,
            worked > 0 ? 1 : 0,
            awayTotal > 0 ? 1 : 0);
    }

    private static string FormatDbDate(DateOnly day) =>
        day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static void RollbackQuietly(SqliteTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch (InvalidOperationException)
        {
        }
    }
}
