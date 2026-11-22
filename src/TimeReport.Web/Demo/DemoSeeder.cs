using System.Security.Cryptography;
using TimeReport.Models;
using TimeReport.Services;
using TimeReport.Web.Auth;
using TimeReport.Web.Security;
using TimeReport.Web.Services;

namespace TimeReport.Web.Demo;

public sealed partial class DemoSeeder
{
    private const string TotpProtectionPurpose = "timereport-totp-v1";

    private readonly AuthDatabase _authDatabase;
    private readonly MasterKey _masterKey;
    private readonly PasswordService _passwords;
    private readonly RecoveryCodeService _recoveryCodes;
    private readonly UserDataStore _userData;
    private readonly DemoInfo _demo;
    private readonly ILogger<DemoSeeder> _logger;

    public DemoSeeder(
        AuthDatabase authDatabase,
        MasterKey masterKey,
        PasswordService passwords,
        RecoveryCodeService recoveryCodes,
        UserDataStore userData,
        DemoInfo demo,
        ILogger<DemoSeeder> logger)
    {
        _authDatabase = authDatabase;
        _masterKey = masterKey;
        _passwords = passwords;
        _recoveryCodes = recoveryCodes;
        _userData = userData;
        _demo = demo;
        _logger = logger;
    }

    public void SeedIfNeeded()
    {
        if (!_demo.Enabled || _authDatabase.CountUsers() > 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var guest = new AuthUser
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = _demo.GuestUsername,
            DisplayName = _demo.GuestDisplayName,
            IsAdmin = true,
            Disabled = false,
            MustChangePassword = false,
            TotpSecret = _masterKey.Protect(DemoInfo.GuestTotpSecret, TotpProtectionPurpose),
            TotpConfirmed = true,
            TotpLastStep = 0,
            Language = "en",
            Theme = "dark",
            TimeZoneId = "UTC",
            OvertimeDailyMinutes = 8 * 60,
            OvertimeWeeklyMinutes = 40 * 60,
            BackupRetentionCount = 20,
            SecurityStamp = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        guest.PasswordHash = _passwords.Hash(guest, _demo.GuestPassword);

        _authDatabase.CreateFirstAdmin(guest);
        _authDatabase.ReplaceRecoveryCodes(
            guest.Id,
            DemoInfo.RecoveryCodes.Select(_recoveryCodes.Hash));
        guest.UnusedRecoveryCodes = DemoInfo.RecoveryCodes.Count;

        using (var session = _userData.Open(guest))
        {
            SeedFakeTimesheet(session.Service);
        }

        _authDatabase.Audit(
            "demo",
            "demo.seeded",
            "guest demo account and fake timesheet data",
            null);
        LogSeeded(_logger, guest.Username);
    }

    private static void SeedFakeTimesheet(TimeReportService service)
    {
        var customerPortal = service.CreateProject("Customer Portal");
        var internalTools = service.CreateProject("Internal Tools");
        var securityProgram = service.CreateProject("Security Program");
        var projects = new[] { customerPortal, internalTools, securityProgram };

        var meeting = service.CreateEntryType("Meeting", EntryKind.Work);
        var training = service.CreateEntryType("Training", EntryKind.Work);

        var types = service.ListEntryTypes();
        var work = DefaultType(types, EntryKind.Work);
        var lunch = DefaultType(types, EntryKind.Lunch);
        var away = DefaultType(types, EntryKind.Away);
        var xpause = DefaultType(types, EntryKind.XPause);

        var today = service.Today;
        var currentWeekStart = TimeUtil.WeekStart(today);
        for (var weekOffset = -7; weekOffset <= 0; weekOffset++)
        {
            var monday = currentWeekStart.AddDays(weekOffset * 7);
            for (var dayOffset = 0; dayOffset < 5; dayOffset++)
            {
                var date = monday.AddDays(dayOffset);
                if (date > today)
                {
                    continue;
                }

                var seed = (weekOffset + 7) * 5 + dayOffset;
                if (seed % 11 == 0)
                {
                    service.SaveEntry(new Entry(
                        null,
                        date,
                        EntryKind.Away,
                        8 * 60,
                        16 * 60 + 30,
                        null,
                        null,
                        "Vacation",
                        away.Id,
                        away.Name,
                        "UTC"));
                    continue;
                }

                var project = projects[seed % projects.Length];
                var workType = seed % 13 == 0
                    ? meeting
                    : seed % 17 == 0
                        ? training
                        : work;
                var start = 8 * 60 + (seed % 2 == 0 ? 0 : 30);
                var end = 16 * 60 + 30 + (seed % 4 == 0 ? 60 : 0);

                service.SaveEntry(new Entry(
                    null,
                    date,
                    EntryKind.Work,
                    start,
                    end,
                    project.Id,
                    null,
                    null,
                    workType.Id,
                    workType.Name,
                    "UTC"));
                service.SaveEntry(new Entry(
                    null,
                    date,
                    EntryKind.Lunch,
                    12 * 60,
                    12 * 60 + 30,
                    null,
                    null,
                    null,
                    lunch.Id,
                    lunch.Name,
                    "UTC"));

                if (seed % 3 == 0)
                {
                    service.SaveEntry(new Entry(
                        null,
                        date,
                        EntryKind.XPause,
                        14 * 60,
                        14 * 60 + 15,
                        null,
                        null,
                        null,
                        xpause.Id,
                        xpause.Name,
                        "UTC"));
                }
            }
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Demo guest seeded: username={Username}; TOTP is shown on the sign-in page.")]
    private static partial void LogSeeded(ILogger logger, string username);

    private static EntryType DefaultType(
        IReadOnlyList<EntryType> types,
        EntryKind kind)
    {
        var defaultName = EntryTypeDisplay.DefaultName(kind);
        return types.FirstOrDefault(type =>
                   type.Category == kind &&
                   string.Equals(type.Name, defaultName, StringComparison.OrdinalIgnoreCase))
               ?? types.First(type => type.Category == kind);
    }
}
