using TimeReport.Web.Auth;
using TimeReport.Web.Configuration;

namespace TimeReport.Web.Demo;

/// <summary>
/// Public, disposable demo configuration. Nothing here is a secret: the guest
/// password, TOTP secret and recovery code are printed on the sign-in page on
/// purpose. Demo data is fake and is destroyed on every restart.
/// </summary>
public sealed class DemoInfo
{
    public const string GuestUsernameValue = "guest";
    public const string GuestPasswordValue = "guest";
    public const string GuestTotpSecret = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";
    public const string GuestRecoveryCodeValue = "DEMO-CODE-0001";

    public static IReadOnlyList<string> RecoveryCodes { get; } =
        Enumerable.Range(1, 10)
            .Select(index => $"DEMO-CODE-{index:0000}")
            .ToList();

    public DemoInfo(WebOptions options)
    {
        Enabled = options.DemoMode;
        ResetHours = options.DemoResetHours;
    }

    public bool Enabled { get; }

    public int ResetHours { get; }

    public string GuestUsername => GuestUsernameValue;

    public string GuestPassword => GuestPasswordValue;

    public string GuestDisplayName => "Demo Guest";

    public string GuestRecoveryCode => GuestRecoveryCodeValue;

    public string Issuer => "TimeReport Demo";

    public bool IsGuest(AuthUser user) =>
        Enabled && string.Equals(user.Username, GuestUsername, StringComparison.OrdinalIgnoreCase);
}
