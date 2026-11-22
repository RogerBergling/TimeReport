using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using TimeReport.Models;
using TimeReport.Web.Auth;
using TimeReport.Web.Configuration;
using TimeReport.Web.Security;
using TimeReport.Web.Services;

namespace TimeReport.Web.Tests;

public sealed class UserDataStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly MasterKey _master;
    private readonly UserDataStore _store;

    public UserDataStoreTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "timereport-user-data-tests",
            Guid.NewGuid().ToString("N"));
        _master = MasterKey.Load(new WebOptions
        {
            MasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            DataDir = _directory,
        });
        _store = new UserDataStore(
            new HttpContextAccessor(),
            new WebOptions { DataDir = _directory },
            _master);
    }

    [Fact]
    public void UsersGetSeparateEncryptedDatabases()
    {
        var alice = NewUser("alice");
        var bob = NewUser("bob");

        using (var session = _store.Open(alice))
        {
            var project = session.Service.CreateProject("Alice private");
            session.Service.SaveEntry(new Entry(
                null,
                new DateOnly(2026, 11, 20),
                EntryKind.Work,
                8 * 60,
                12 * 60,
                project.Id));
        }

        using (var session = _store.Open(bob))
        {
            Assert.Empty(session.Service.ListProjects());
            Assert.Empty(session.Service.ListEntries(
                new DateOnly(2026, 11, 20),
                new DateOnly(2026, 11, 20)));
        }

        using (var session = _store.Open(alice))
        {
            Assert.Single(session.Service.ListProjects());
            Assert.Single(session.Service.ListEntries(
                new DateOnly(2026, 11, 20),
                new DateOnly(2026, 11, 20)));
        }

        var alicePath = Path.Combine(WebPaths.UserDirectory(_directory, alice.Id), "timereport.db");
        var bobPath = Path.Combine(WebPaths.UserDirectory(_directory, bob.Id), "timereport.db");
        var aliceHeader = ReadHeader(alicePath);
        var bobHeader = ReadHeader(bobPath);
        Assert.False(aliceHeader.SequenceEqual(bobHeader));
        Assert.False(aliceHeader.SequenceEqual("SQLite format 3\0"u8.ToArray()));
    }

    [Fact]
    public void DeletingUserDataRemovesTheDirectory()
    {
        var user = NewUser("carol");
        using (var session = _store.Open(user))
        {
            session.Service.CreateProject("Temporary");
        }
        Assert.True(WebPaths.UserDirectoryExists(_directory, user.Id));

        _store.DeleteUserData(user.Id);
        Assert.False(WebPaths.UserDirectoryExists(_directory, user.Id));
    }

    public void Dispose()
    {
        _store.Dispose();
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

    private static AuthUser NewUser(string username) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Username = username,
        DisplayName = username,
        SecurityStamp = "stamp",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
        TimeZoneId = "UTC",
    };

    private static byte[] ReadHeader(string path)
    {
        using var stream = File.OpenRead(path);
        var header = new byte[16];
        _ = stream.Read(header);
        return header;
    }
}
