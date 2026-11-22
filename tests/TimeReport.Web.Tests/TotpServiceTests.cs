using TimeReport.Web.Auth;

namespace TimeReport.Web.Tests;

public sealed class TotpServiceTests
{
    // RFC 6238 test secret: ASCII "12345678901234567890".
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1111111111, "050471")]
    [InlineData(1234567890, "005924")]
    [InlineData(2000000000, "279037")]
    [InlineData(20000000000, "353130")]
    public void ValidatesRfc6238Vectors(long unixTime, string expectedCode)
    {
        var service = new TotpService();
        var result = service.Validate(
            RfcSecret,
            expectedCode,
            lastAcceptedStep: 0,
            DateTimeOffset.FromUnixTimeSeconds(unixTime));

        Assert.True(result.Success);
        Assert.Equal(unixTime / 30, result.Step);
    }

    [Fact]
    public void RejectsReplayAndWrongCodes()
    {
        var service = new TotpService();
        var now = DateTimeOffset.FromUnixTimeSeconds(1234567890);
        var code = "005924";

        Assert.True(service.Validate(RfcSecret, code, 0, now).Success);
        Assert.False(service.Validate(RfcSecret, code, 1234567890 / 30, now).Success);
        Assert.False(service.Validate(RfcSecret, "000000", 0, now).Success);
        Assert.False(service.Validate(RfcSecret, "12345", 0, now).Success);
    }

    [Fact]
    public void GeneratesCurrentCodeForDemoAndAuthenticatorDisplay()
    {
        var service = new TotpService();
        Assert.Equal(
            "287082",
            service.GenerateCode(RfcSecret, DateTimeOffset.FromUnixTimeSeconds(59)));
        Assert.Equal(
            "005924",
            service.GenerateCode(RfcSecret, DateTimeOffset.FromUnixTimeSeconds(1234567890)));
    }

    [Fact]
    public void GeneratesBase32SecretAndQrDataUrl()
    {
        var service = new TotpService();
        var secret = service.GenerateSecret();
        Assert.Equal(32, secret.Length);
        Assert.All(secret, character => Assert.Contains(character, "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"));

        var uri = service.BuildUri("TimeReport", "alice", secret);
        Assert.StartsWith("otpauth://totp/TimeReport:alice?", uri);
        Assert.Contains($"secret={secret}", uri);

        var qr = service.QrCodeDataUrl(uri);
        Assert.StartsWith("data:image/png;base64,", qr);
        Assert.True(qr.Length > 200);
    }
}
