using System.Security.Cryptography;
using TimeReport.Web.Auth;
using TimeReport.Web.Configuration;
using TimeReport.Web.Security;

namespace TimeReport.Web.Tests;

public sealed class RecoveryCodeTests
{
    [Fact]
    public void GeneratesTenDistinctHashedCodes()
    {
        var master = MasterKey.Load(new WebOptions
        {
            MasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        });
        var service = new RecoveryCodeService(master);
        var codes = service.Generate();

        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Select(item => item.Plain).Distinct().Count());
        foreach (var (plain, hash) in codes)
        {
            Assert.Matches("^[A-Z2-7]{4}-[A-Z2-7]{4}-[A-Z2-7]{4}$", plain);
            Assert.True(service.Matches(plain, hash));
            Assert.False(service.Matches("AAAA-AAAA-AAAA", hash));
        }
    }

    [Fact]
    public void NormalizesInput()
    {
        Assert.Equal("ABCDEFGHJKLM", RecoveryCodeService.Normalize("abcd-efgh jklm"));
        Assert.True(RecoveryCodeService.IsWellFormed("abcd-efgh-jklm"));
        Assert.False(RecoveryCodeService.IsWellFormed("short"));
    }
}
