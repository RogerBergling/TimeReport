using System.Security.Cryptography;
using System.Text;
using TimeReport.Web.Security;

namespace TimeReport.Web.Auth;

public sealed class RecoveryCodeService
{
    public const int CodeCount = 10;
    private const int CodeBytes = 8;
    private const string Purpose = "timereport-recovery-codes-v1";

    private readonly MasterKey _masterKey;

    public RecoveryCodeService(MasterKey masterKey)
    {
        _masterKey = masterKey;
    }

    public IReadOnlyList<(string Plain, string Hash)> Generate(int count = CodeCount)
    {
        var result = new List<(string Plain, string Hash)>(count);
        for (var index = 0; index < count; index++)
        {
            var raw = Base32.Encode(RandomNumberGenerator.GetBytes(CodeBytes));
            var code = raw[..12];
            var formatted = $"{code[..4]}-{code[4..8]}-{code[8..12]}";
            result.Add((formatted, Hash(formatted)));
        }
        return result;
    }

    public string Hash(string code) =>
        Convert.ToBase64String(
            HMACSHA256.HashData(
                _masterKey.Derive(Purpose),
                Encoding.UTF8.GetBytes(Normalize(code))));

    public bool Matches(string code, string expectedHash) =>
        MasterKey.FixedTimeEquals(Hash(code), expectedHash);

    public static string Normalize(string? code) => Base32.Normalize(code);

    public static bool IsWellFormed(string? code) =>
        Normalize(code).Length == 12;
}
