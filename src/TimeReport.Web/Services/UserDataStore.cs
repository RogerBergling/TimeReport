using System.Collections.Concurrent;
using TimeReport.Data;
using TimeReport.Models;
using TimeReport.Services;
using TimeReport.Web.Auth;
using TimeReport.Web.Configuration;
using TimeReport.Web.Security;

namespace TimeReport.Web.Services;

public sealed class UserDataSession : IDisposable
{
    public UserDataSession(Database database, TimeReportService service)
    {
        Database = database;
        Service = service;
    }

    public Database Database { get; }

    public TimeReportService Service { get; }

    public void Dispose() => Database.Dispose();
}

/// <summary>
/// Opens the current user's private encrypted database. The connection is
/// scoped to one request and disposed by the DI container.
/// </summary>
public sealed class UserDataStore : IDisposable
{
    private const string UserDatabasePurpose = "timereport-user-db-v1";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> OpenLocks = new(StringComparer.Ordinal);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly WebOptions _options;
    private readonly MasterKey _masterKey;
    private UserDataSession? _session;

    public UserDataStore(
        IHttpContextAccessor httpContextAccessor,
        WebOptions options,
        MasterKey masterKey)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options;
        _masterKey = masterKey;
    }

    public AuthUser User =>
        _httpContextAccessor.HttpContext?.GetAuthUser()
        ?? throw new InvalidOperationException("There is no authenticated user for this request.");

    public UserDataSession Current => _session ??= Open(User);

    public UserDataSession Open(AuthUser user)
    {
        // Serialize the open/migration step for one account. Normal requests
        // use separate connections after this; SQLite WAL handles those.
        var gate = OpenLocks.GetOrAdd(user.Id, _ => new SemaphoreSlim(1, 1));
        gate.Wait();
        try
        {
            var directory = WebPaths.UserDirectory(_options.DataDir, user.Id);
            WebPaths.EnsurePrivateDirectory(directory);
            var path = Path.Combine(directory, "timereport.db");
            var key = _masterKey.DeriveBase64(UserDatabasePurpose, user.Id);
            var database = new Database(path, key)
            {
                EnableWal = true,
                CipherMemorySecurity = true,
                BackupRetentionCount = Math.Clamp(user.BackupRetentionCount, 1, 365),
            };

            try
            {
                database.Open();
            }
            catch
            {
                database.Dispose();
                throw;
            }

            var service = new TimeReportService(database)
            {
                TimeZoneId = TimeZoneService.ResolveId(user.TimeZoneId),
                OvertimePolicy = new OvertimePolicy(
                    Math.Max(0, user.OvertimeDailyMinutes),
                    Math.Max(0, user.OvertimeWeeklyMinutes)),
            };

            return new UserDataSession(database, service);
        }
        finally
        {
            gate.Release();
        }
    }

    public void DeleteUserData(string userId) =>
        WebPaths.DeleteUserDirectory(_options.DataDir, userId);

    public void Dispose() => _session?.Dispose();
}
