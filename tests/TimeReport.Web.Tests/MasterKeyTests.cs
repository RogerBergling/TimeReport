using System.Security.Cryptography;
using TimeReport.Web.Configuration;
using TimeReport.Web.Security;

namespace TimeReport.Web.Tests;

public sealed class MasterKeyTests
{
    [Fact]
    public void DerivesDifferentKeysAndProtectsSecrets()
    {
        var raw = RandomNumberGenerator.GetBytes(32);
        var options = new WebOptions
        {
            MasterKey = Convert.ToBase64String(raw),
        };
        var master = MasterKey.Load(options);

        var auth = master.DeriveBase64("timereport-auth-db-v1");
        var userA = master.DeriveBase64("timereport-user-db-v1", "a");
        var userB = master.DeriveBase64("timereport-user-db-v1", "b");

        Assert.NotEqual(auth, userA);
        Assert.NotEqual(userA, userB);

        var protectedValue = master.Protect("hello", "timereport-test-v1");
        Assert.StartsWith("v1:", protectedValue);
        Assert.Equal("hello", master.Unprotect(protectedValue, "timereport-test-v1"));
        Assert.ThrowsAny<CryptographicException>(
            () => master.Unprotect(protectedValue[..^2] + "AA", "timereport-test-v1"));
    }

    [Fact]
    public void AcceptsHexadecimalKeys()
    {
        var raw = RandomNumberGenerator.GetBytes(32);
        var fromHex = MasterKey.Load(new WebOptions
        {
            MasterKey = Convert.ToHexString(raw),
        });
        var fromBase64 = MasterKey.Load(new WebOptions
        {
            MasterKey = Convert.ToBase64String(raw),
        });
        Assert.Equal(
            fromBase64.DeriveBase64("timereport-hex-test"),
            fromHex.DeriveBase64("timereport-hex-test"));
    }

    [Fact]
    public void RejectsMissingAndShortKeys()
    {
        Assert.Throws<InvalidOperationException>(() => MasterKey.Load(new WebOptions()));
        Assert.Throws<InvalidOperationException>(() => MasterKey.Load(new WebOptions
        {
            MasterKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)),
        }));
    }
}
