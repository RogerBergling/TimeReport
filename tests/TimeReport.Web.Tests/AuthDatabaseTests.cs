using TimeReport.Web.Auth;
using TimeReport.Web.Configuration;
using TimeReport.Web.Security;

namespace TimeReport.Web.Tests;

public sealed class AuthDatabaseTests : IDisposable
{
    private readonly string _directory;
    private readonly AuthDatabase _database;

    public AuthDatabaseTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "timereport-auth-tests",
            Guid.NewGuid().ToString("N"));
        var master = MasterKey.Load(new WebOptions
        {
            MasterKey = Convert.ToBase64String(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            DataDir = _directory,
        });
        _database = new AuthDatabase(
            Path.Combine(_directory, "auth.db"),
            master.DeriveBase64("timereport-auth-db-v1"));
        _database.Initialize();
    }

    [Fact]
    public void CreatesAndReadsUsers()
    {
        var user = NewUser("alice");
        _database.CreateUser(user);

        var loaded = _database.GetUserByUsername("ALICE");
        Assert.NotNull(loaded);
        Assert.Equal(user.Id, loaded!.Id);
        Assert.Equal("Alice", loaded.DisplayName);
        Assert.Equal(0, loaded.UnusedRecoveryCodes);

        Assert.Throws<AuthDatabaseException>(() => _database.CreateUser(NewUser("alice")));
    }

    [Fact]
    public void FirstAdminCanOnlyBeCreatedOnce()
    {
        _database.CreateFirstAdmin(NewUser("admin", isAdmin: true));
        Assert.Throws<AuthDatabaseException>(
            () => _database.CreateFirstAdmin(NewUser("other-admin", isAdmin: true)));
    }

    [Fact]
    public void TracksTotpReplayAndRecoveryCodes()
    {
        var user = NewUser("bob");
        _database.CreateUser(user);
        _database.SetTotpSecret(user.Id, "protected");
        Assert.True(_database.TryAdvanceTotpStep(user.Id, 100));
        Assert.False(_database.TryAdvanceTotpStep(user.Id, 100));
        Assert.True(_database.TryAdvanceTotpStep(user.Id, 101));

        _database.ReplaceRecoveryCodes(user.Id, ["hash-1", "hash-2"]);
        Assert.Equal(2, _database.CountUnusedRecoveryCodes(user.Id));
        Assert.False(_database.ConsumeRecoveryCode(user.Id, "unknown"));
        Assert.True(_database.ConsumeRecoveryCode(user.Id, "hash-1"));
        Assert.False(_database.ConsumeRecoveryCode(user.Id, "hash-1"));
        Assert.Equal(1, _database.CountUnusedRecoveryCodes(user.Id));
    }

    [Fact]
    public void PendingMfaExpiresAndAttemptsIncrement()
    {
        var user = NewUser("carol");
        _database.CreateUser(user);
        _database.CreatePendingMfa("hash", user.Id, DateTimeOffset.UtcNow.AddMinutes(5));

        var pending = _database.GetPendingMfa("hash");
        Assert.NotNull(pending);
        Assert.Equal(user.Id, pending!.UserId);
        Assert.Equal(1, _database.IncrementPendingMfaAttempts("hash"));
        Assert.Equal(2, _database.IncrementPendingMfaAttempts("hash"));

        _database.DeletePendingMfa("hash");
        Assert.Null(_database.GetPendingMfa("hash"));

        _database.CreatePendingMfa("expired", user.Id, DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Null(_database.GetPendingMfa("expired"));
    }

    [Fact]
    public void CredentialChangesRevokePendingMfa()
    {
        var user = NewUser("dave");
        _database.CreateUser(user);

        _database.CreatePendingMfa("p1", user.Id, DateTimeOffset.UtcNow.AddMinutes(5));
        _database.SetPassword(user.Id, "new-hash", mustChange: false, "stamp-1");
        Assert.Null(_database.GetPendingMfa("p1"));

        _database.CreatePendingMfa("p2", user.Id, DateTimeOffset.UtcNow.AddMinutes(5));
        _database.ResetTotp(user.Id, "stamp-2");
        Assert.Null(_database.GetPendingMfa("p2"));

        _database.CreatePendingMfa("p3", user.Id, DateTimeOffset.UtcNow.AddMinutes(5));
        _database.SetDisabled(user.Id, true, "stamp-3");
        Assert.Null(_database.GetPendingMfa("p3"));
    }

    [Fact]
    public void ProtectsLastActiveAdmin()
    {
        var admin = NewUser("admin", isAdmin: true);
        _database.CreateUser(admin);
        Assert.Equal(1, _database.CountActiveAdmins());
        Assert.Equal(0, _database.CountActiveAdmins(admin.Id));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static AuthUser NewUser(string username, bool isAdmin = false) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Username = username,
        DisplayName = char.ToUpperInvariant(username[0]) + username[1..],
        PasswordHash = "not-used-in-this-test",
        IsAdmin = isAdmin,
        SecurityStamp = Guid.NewGuid().ToString("N"),
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };
}
