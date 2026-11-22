using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TimeReport.Services;
using TimeReport.Web.Configuration;
using TimeReport.Web.Demo;
using TimeReport.Web.Security;

namespace TimeReport.Web.Auth;

public sealed class AuthException : Exception
{
    public AuthException(string message) : base(message)
    {
    }
}

public enum PasswordLoginStatus
{
    Success,
    Invalid,
    Disabled,
    Throttled,
}

public sealed record PasswordLoginResult(PasswordLoginStatus Status, AuthUser? User);

public sealed partial class AuthService
{
    private const string TotpProtectionPurpose = "timereport-totp-v1";
    private const string PendingMfaPurpose = "timereport-pending-mfa-v1";
    private static readonly object AdminMutationLock = new();

    private readonly AuthDatabase _database;
    private readonly PasswordService _passwords;
    private readonly TotpService _totp;
    private readonly RecoveryCodeService _recoveryCodes;
    private readonly LoginThrottle _throttle;
    private readonly TimeProvider _timeProvider;
    private readonly WebOptions _options;
    private readonly MasterKey _masterKey;
    private readonly DemoInfo _demo;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AuthDatabase database,
        PasswordService passwords,
        TotpService totp,
        RecoveryCodeService recoveryCodes,
        LoginThrottle throttle,
        TimeProvider timeProvider,
        WebOptions options,
        MasterKey masterKey,
        DemoInfo demo,
        ILogger<AuthService> logger)
    {
        _database = database;
        _passwords = passwords;
        _totp = totp;
        _recoveryCodes = recoveryCodes;
        _throttle = throttle;
        _timeProvider = timeProvider;
        _options = options;
        _masterKey = masterKey;
        _demo = demo;
        _logger = logger;
    }

    public AuthUser? GetUserById(string id) => _database.GetUserById(id);

    public AuthUser? GetUserByUsername(string username) =>
        _database.GetUserByUsername(NormalizeUsername(username));

    public IReadOnlyList<AuthUser> ListUsers() => _database.ListUsers();

    public int CountUsers() => _database.CountUsers();

    public PasswordLoginResult AuthenticatePassword(
        string? username,
        string? password,
        string? ipAddress)
    {
        var normalized = NormalizeUsername(username);
        var window = TimeSpan.FromMinutes(_options.LoginWindowMinutes);
        if (normalized.Length > 64)
        {
            _passwords.VerifyDummy(password ?? string.Empty);
            _throttle.RecordFailure($"ip:{ipAddress ?? "unknown"}", window);
            Audit(null, "login.failed", "invalid username", ipAddress);
            return new PasswordLoginResult(PasswordLoginStatus.Invalid, null);
        }

        var keys = ThrottleKeys(normalized, ipAddress);
        if (keys.Any(key => _throttle.IsBlocked(key, _options.MaxLoginAttemptsPerUser, window)) ||
            (ipAddress is not null &&
             _throttle.IsBlocked($"ip:{ipAddress}", _options.MaxLoginAttemptsPerIp, window)))
        {
            Audit(null, "login.throttled", "username throttled", ipAddress);
            return new PasswordLoginResult(PasswordLoginStatus.Throttled, null);
        }

        var user = normalized.Length == 0 ? null : _database.GetUserByUsername(normalized);
        if (user is null)
        {
            _passwords.VerifyDummy(password ?? string.Empty);
            RecordFailure(keys, ipAddress, window);
            Audit(null, "login.failed", "unknown user", ipAddress);
            return new PasswordLoginResult(PasswordLoginStatus.Invalid, null);
        }

        var verification = _passwords.Verify(user, password ?? string.Empty);
        if (verification == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
        {
            RecordFailure(keys, ipAddress, window);
            Audit(user.Username, "login.failed", "password mismatch", ipAddress);
            return new PasswordLoginResult(PasswordLoginStatus.Invalid, null);
        }

        if (user.Disabled)
        {
            RecordFailure(keys, ipAddress, window);
            Audit(user.Username, "login.disabled", null, ipAddress);
            return new PasswordLoginResult(PasswordLoginStatus.Disabled, null);
        }

        _throttle.Reset($"pair:{ipAddress ?? "unknown"}|{user.Username.ToLowerInvariant()}");
        _throttle.Reset($"user:{user.Username.ToLowerInvariant()}");
        Audit(user.Username, "login.password_ok", null, ipAddress);
        return new PasswordLoginResult(PasswordLoginStatus.Success, user);
    }

    public string CreatePendingMfa(AuthUser user)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        var hash = _masterKey.HashToken(token, PendingMfaPurpose);
        var expires = _timeProvider.GetUtcNow().AddMinutes(_options.MfaMinutes);
        _database.CreatePendingMfa(hash, user.Id, expires);
        return token;
    }

    public (PendingMfa Pending, AuthUser User)? GetPendingMfa(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var pending = _database.GetPendingMfa(_masterKey.HashToken(token, PendingMfaPurpose));
        if (pending is null)
        {
            return null;
        }

        var user = _database.GetUserById(pending.UserId);
        return user is null || user.Disabled ? null : (pending, user);
    }

    public int RecordMfaFailure(PendingMfa pending) =>
        _database.IncrementPendingMfaAttempts(pending.TokenHash);

    public void ClearPendingMfa(PendingMfa pending) =>
        _database.DeletePendingMfa(pending.TokenHash);

    public bool VerifyMfa(AuthUser user, string? code, string? ipAddress)
    {
        if (!user.TotpConfirmed || string.IsNullOrWhiteSpace(user.TotpSecret))
        {
            return false;
        }

        TotpValidationResult result;
        try
        {
            var secret = UnprotectSecret(user.TotpSecret);
            result = _totp.Validate(secret, code, user.TotpLastStep, _timeProvider.GetUtcNow());
        }
        catch (CryptographicException)
        {
            Audit(user.Username, "mfa.error", "TOTP secret could not be decrypted", ipAddress);
            return false;
        }

        if (!result.Success || !_database.TryAdvanceTotpStep(user.Id, result.Step))
        {
            Audit(user.Username, "mfa.failed", "invalid or replayed code", ipAddress);
            return false;
        }

        Audit(user.Username, "mfa.ok", null, ipAddress);
        return true;
    }

    public bool VerifyRecoveryCode(AuthUser user, string? code, string? ipAddress)
    {
        if (!RecoveryCodeService.IsWellFormed(code))
        {
            Audit(user.Username, "recovery.failed", "malformed code", ipAddress);
            return false;
        }

        var hash = _recoveryCodes.Hash(code!);
        if (!_database.ConsumeRecoveryCode(user.Id, hash))
        {
            Audit(user.Username, "recovery.failed", "unknown or used code", ipAddress);
            return false;
        }

        Audit(user.Username, "recovery.ok", null, ipAddress);
        return true;
    }

    public string BeginTotpEnrollment(AuthUser user)
    {
        EnsureNotDemoGuest(user);
        if (user.TotpConfirmed)
        {
            throw new AuthException("Two-factor authentication is already enabled.");
        }

        if (!string.IsNullOrWhiteSpace(user.TotpSecret))
        {
            try
            {
                return UnprotectSecret(user.TotpSecret);
            }
            catch (CryptographicException)
            {
                // Replace an unreadable half-finished enrollment.
            }
        }

        var secret = _totp.GenerateSecret();
        var protectedSecret = ProtectSecret(secret);
        _database.SetTotpSecret(user.Id, protectedSecret);
        user.TotpSecret = protectedSecret;
        user.TotpConfirmed = false;
        user.TotpLastStep = 0;
        return secret;
    }

    public AuthUser ConfirmTotp(AuthUser user, string? code)
    {
        EnsureNotDemoGuest(user);
        if (user.TotpConfirmed)
        {
            throw new AuthException("Two-factor authentication is already enabled.");
        }

        if (string.IsNullOrWhiteSpace(user.TotpSecret))
        {
            throw new AuthException("Start the authenticator setup again.");
        }

        var secret = UnprotectSecret(user.TotpSecret);
        var result = _totp.Validate(secret, code, user.TotpLastStep, _timeProvider.GetUtcNow());
        if (!result.Success)
        {
            throw new AuthException("The authenticator code is not valid. Check the clock and try again.");
        }

        var newStamp = NewSecurityStamp();
        _database.ConfirmTotp(user.Id, result.Step, newStamp);
        user.TotpConfirmed = true;
        user.TotpLastStep = result.Step;
        user.SecurityStamp = newStamp;
        user.UpdatedAtUtc = _timeProvider.GetUtcNow();
        Audit(user.Username, "mfa.enrolled", null, null);
        return user;
    }

    public IReadOnlyList<string> RegenerateRecoveryCodes(AuthUser user)
    {
        EnsureNotDemoGuest(user);
        var codes = _recoveryCodes.Generate();
        _database.ReplaceRecoveryCodes(user.Id, codes.Select(item => item.Hash));
        user.UnusedRecoveryCodes = codes.Count;
        Audit(user.Username, "recovery.generated", $"{codes.Count} codes", null);
        return codes.Select(item => item.Plain).ToList();
    }

    public AuthUser ChangePassword(AuthUser user, string? currentPassword, string? newPassword)
    {
        EnsureNotDemoGuest(user);
        if (!VerifyCurrentPassword(user, currentPassword, "password-change", null))
        {
            throw new AuthException("The current password is not correct.");
        }

        if (_passwords.Verify(user, newPassword ?? string.Empty) !=
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
        {
            throw new AuthException("Choose a password that is different from the current one.");
        }

        var error = PasswordPolicy.Validate(newPassword, user.Username);
        if (error is not null)
        {
            throw new AuthException(error);
        }

        var hash = _passwords.Hash(user, newPassword!);
        var stamp = NewSecurityStamp();
        _database.SetPassword(user.Id, hash, mustChange: false, stamp);
        user.PasswordHash = hash;
        user.MustChangePassword = false;
        user.SecurityStamp = stamp;
        user.UpdatedAtUtc = _timeProvider.GetUtcNow();
        Audit(user.Username, "password.changed", null, null);
        return user;
    }

    public bool VerifyCurrentPassword(
        AuthUser user,
        string? password,
        string purpose,
        string? ipAddress)
    {
        var key = $"reauth:{purpose}:{user.Id}";
        var window = TimeSpan.FromMinutes(_options.LoginWindowMinutes);
        if (_throttle.IsBlocked(key, 10, window))
        {
            return false;
        }

        var verification = _passwords.Verify(user, password ?? string.Empty);
        if (verification == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
        {
            _throttle.RecordFailure(key, window);
            Audit(user.Username, "reauth.failed", purpose, ipAddress);
            return false;
        }

        _throttle.Reset(key);
        return true;
    }

    public AuthUser CreateFirstAdmin(
        string? username,
        string? displayName,
        string? password,
        string? ipAddress)
    {
        var user = BuildNewUser(username, displayName, password, isAdmin: true, mustChange: false, out _);
        _database.CreateFirstAdmin(user);
        Audit(user.Username, "setup.completed", "first administrator created", ipAddress);
        return user;
    }

    public (AuthUser User, string? TemporaryPassword) CreateUser(
        AuthUser admin,
        string? username,
        string? displayName,
        string? password,
        bool isAdmin,
        string? ipAddress)
    {
        var user = BuildNewUser(username, displayName, password, isAdmin, mustChange: true, out var temporary);
        _database.CreateUser(user);
        Audit(admin.Username, "user.created", $"{user.Username}; admin={isAdmin}", ipAddress);
        return (user, temporary);
    }

    public string ResetPassword(AuthUser admin, AuthUser target, string? ipAddress)
    {
        EnsureNotDemoGuest(target);
        var temporary = GenerateTemporaryPassword();
        var hash = _passwords.Hash(target, temporary);
        var stamp = NewSecurityStamp();
        _database.SetPassword(target.Id, hash, mustChange: true, stamp);
        target.PasswordHash = hash;
        target.MustChangePassword = true;
        target.SecurityStamp = stamp;
        target.UpdatedAtUtc = _timeProvider.GetUtcNow();
        Audit(admin.Username, "user.password_reset", target.Username, ipAddress);
        return temporary;
    }

    public void ResetTotp(AuthUser admin, AuthUser target, string? ipAddress)
    {
        EnsureNotDemoGuest(target);
        var stamp = NewSecurityStamp();
        _database.ResetTotp(target.Id, stamp);
        target.TotpSecret = null;
        target.TotpConfirmed = false;
        target.TotpLastStep = 0;
        target.SecurityStamp = stamp;
        target.UnusedRecoveryCodes = 0;
        target.UpdatedAtUtc = _timeProvider.GetUtcNow();
        Audit(admin.Username, "user.mfa_reset", target.Username, ipAddress);
    }

    public void SetDisabled(AuthUser admin, AuthUser target, bool disabled, string? ipAddress)
    {
        EnsureNotDemoGuest(target);
        lock (AdminMutationLock)
        {
            target = RefreshWithinLock(target);
            EnsureNotDemoGuest(target);
            if (disabled && admin.Id == target.Id)
            {
                throw new AuthException("You cannot disable your own account.");
            }
            if (disabled)
            {
                EnsureNotLastAdminIfNeeded(target, "disable");
            }

            var stamp = NewSecurityStamp();
            _database.SetDisabled(target.Id, disabled, stamp);
            target.Disabled = disabled;
            target.SecurityStamp = stamp;
            target.UpdatedAtUtc = _timeProvider.GetUtcNow();
            Audit(admin.Username, disabled ? "user.disabled" : "user.enabled", target.Username, ipAddress);
        }
    }

    public void SetAdmin(AuthUser admin, AuthUser target, bool isAdmin, string? ipAddress)
    {
        EnsureNotDemoGuest(target);
        lock (AdminMutationLock)
        {
            target = RefreshWithinLock(target);
            EnsureNotDemoGuest(target);
            if (!isAdmin && admin.Id == target.Id)
            {
                throw new AuthException("You cannot remove your own administrator role.");
            }
            if (!isAdmin)
            {
                EnsureNotLastAdminIfNeeded(target, "demote");
            }

            var stamp = NewSecurityStamp();
            _database.SetAdmin(target.Id, isAdmin, stamp);
            target.IsAdmin = isAdmin;
            target.SecurityStamp = stamp;
            target.UpdatedAtUtc = _timeProvider.GetUtcNow();
            Audit(admin.Username, isAdmin ? "user.promoted" : "user.demoted", target.Username, ipAddress);
        }
    }

    public void DeleteUser(AuthUser admin, AuthUser target, string? ipAddress)
    {
        EnsureNotDemoGuest(target);
        lock (AdminMutationLock)
        {
            target = RefreshWithinLock(target);
            EnsureNotDemoGuest(target);
            if (admin.Id == target.Id)
            {
                throw new AuthException("You cannot delete your own account.");
            }
            EnsureNotLastAdminIfNeeded(target, "delete");

            _database.DeleteUser(target.Id);
            Audit(admin.Username, "user.deleted", target.Username, ipAddress);
        }
    }

    public AuthUser UpdatePreferences(
        AuthUser user,
        string? displayName,
        string? language,
        string? theme,
        string? timeZoneId,
        int overtimeDailyMinutes,
        int overtimeWeeklyMinutes,
        int backupRetentionCount)
    {
        var cleanedName = CleanDisplayName(displayName, user.Username);
        var cleanedLanguage = string.Equals(language, "sv", StringComparison.OrdinalIgnoreCase) ? "sv" : "en";
        var cleanedTheme = theme is "light" or "dark" or "system" ? theme : "dark";
        var cleanedTimeZone = TimeZoneService.IsValid(timeZoneId)
            ? TimeZoneService.ResolveId(timeZoneId)
            : user.TimeZoneId;
        var daily = Math.Clamp(overtimeDailyMinutes, 0, 24 * 60);
        var weekly = Math.Clamp(overtimeWeeklyMinutes, 0, 7 * 24 * 60);
        var retention = Math.Clamp(backupRetentionCount, 1, 365);

        _database.UpdatePreferences(
            user.Id,
            cleanedName,
            cleanedLanguage,
            cleanedTheme,
            cleanedTimeZone,
            daily,
            weekly,
            retention);

        user.DisplayName = cleanedName;
        user.Language = cleanedLanguage;
        user.Theme = cleanedTheme;
        user.TimeZoneId = cleanedTimeZone;
        user.OvertimeDailyMinutes = daily;
        user.OvertimeWeeklyMinutes = weekly;
        user.BackupRetentionCount = retention;
        user.UpdatedAtUtc = _timeProvider.GetUtcNow();
        Audit(user.Username, "settings.updated", null, null);
        return user;
    }

    public void Audit(string? actor, string action, string? detail, string? ipAddress)
    {
        try
        {
            _database.Audit(actor, action, detail, ipAddress);
        }
        catch (Exception exception)
        {
            LogAuditFailure(_logger, action, exception);
        }
    }

    private AuthUser BuildNewUser(
        string? username,
        string? displayName,
        string? password,
        bool isAdmin,
        bool mustChange,
        out string? temporaryPassword)
    {
        var cleanedUsername = NormalizeUsername(username);
        ValidateUsername(cleanedUsername);
        var cleanedPassword = password;
        temporaryPassword = null;
        if (string.IsNullOrEmpty(cleanedPassword))
        {
            cleanedPassword = GenerateTemporaryPassword();
            temporaryPassword = cleanedPassword;
            mustChange = true;
        }

        var error = PasswordPolicy.Validate(cleanedPassword, cleanedUsername);
        if (error is not null)
        {
            throw new AuthException(error);
        }

        var now = _timeProvider.GetUtcNow();
        var user = new AuthUser
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = cleanedUsername,
            DisplayName = CleanDisplayName(displayName, cleanedUsername),
            IsAdmin = isAdmin,
            MustChangePassword = mustChange,
            Language = "en",
            Theme = "dark",
            TimeZoneId = TimeZoneService.ResolveId(null),
            BackupRetentionCount = 20,
            SecurityStamp = NewSecurityStamp(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        user.PasswordHash = _passwords.Hash(user, cleanedPassword);
        return user;
    }

    private static void ValidateUsername(string username)
    {
        if (!UsernamePattern().IsMatch(username))
        {
            throw new AuthException(
                "Usernames must be 3–64 characters, start with a letter or digit, " +
                "and contain only letters, digits, dot, underscore or hyphen.");
        }
    }

    private static string NormalizeUsername(string? username) =>
        (username ?? string.Empty).Trim();

    private static string CleanDisplayName(string? displayName, string username)
    {
        var withoutControls = new string(
            (displayName ?? string.Empty).Where(character => !char.IsControl(character)).ToArray());
        var cleaned = string.Join(
            " ",
            withoutControls.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
        if (cleaned.Length == 0)
        {
            return username;
        }
        if (cleaned.Length > 80)
        {
            throw new AuthException("Display names must be 80 characters or fewer.");
        }
        return cleaned;
    }

    private void EnsureNotDemoGuest(AuthUser user)
    {
        if (_demo.Enabled && _demo.IsGuest(user))
        {
            throw new AuthException(
                "The demo guest account has fixed credentials. " +
                "Create another user to test credential changes.");
        }
    }

    private AuthUser RefreshWithinLock(AuthUser target) =>
        _database.GetUserById(target.Id)
        ?? throw new AuthException("The user no longer exists.");

    private void EnsureNotLastAdminIfNeeded(AuthUser target, string action)
    {
        if (!target.IsAdmin || target.Disabled)
        {
            return;
        }

        if (_database.CountActiveAdmins(target.Id) == 0)
        {
            throw new AuthException(
                $"You cannot {action} the last active administrator.");
        }
    }

    private void RecordFailure(IEnumerable<string> keys, string? ipAddress, TimeSpan window)
    {
        foreach (var key in keys)
        {
            _throttle.RecordFailure(key, window);
        }
        if (ipAddress is not null)
        {
            _throttle.RecordFailure($"ip:{ipAddress}", window);
        }
    }

    private static IReadOnlyList<string> ThrottleKeys(string username, string? ipAddress)
    {
        var normalized = username.ToLowerInvariant();
        return
        [
            $"user:{normalized}",
            $"pair:{ipAddress ?? "unknown"}|{normalized}",
        ];
    }

    private string ProtectSecret(string plaintext) =>
        _masterKey.Protect(plaintext, TotpProtectionPurpose);

    private string UnprotectSecret(string protectedSecret) =>
        _masterKey.Unprotect(protectedSecret, TotpProtectionPurpose);

    private static string NewSecurityStamp() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(20);
        var chars = new char[bytes.Length];
        for (var index = 0; index < bytes.Length; index++)
        {
            chars[index] = alphabet[bytes[index] % alphabet.Length];
        }
        return new string(chars);
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.-]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Warning,
        Message = "Could not write an audit event {Action}.")]
    private static partial void LogAuditFailure(ILogger logger, string action, Exception exception);
}
