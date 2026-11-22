namespace TimeReport.Web.Auth;

public sealed class AuthUser
{
    public required string Id { get; init; }

    public required string Username { get; init; }

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    public bool Disabled { get; set; }

    public bool MustChangePassword { get; set; }

    public string? TotpSecret { get; set; }

    public bool TotpConfirmed { get; set; }

    public long TotpLastStep { get; set; }

    public string Language { get; set; } = "en";

    public string Theme { get; set; } = "dark";

    public string TimeZoneId { get; set; } = "UTC";

    public int OvertimeDailyMinutes { get; set; }

    public int OvertimeWeeklyMinutes { get; set; }

    public int BackupRetentionCount { get; set; } = 20;

    public string SecurityStamp { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public int UnusedRecoveryCodes { get; set; }
}

public sealed record PendingMfa(
    string TokenHash,
    string UserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int Attempts);

public sealed record RecoveryCode(
    long Id,
    string UserId,
    string CodeHash,
    DateTimeOffset? UsedAtUtc);

public sealed record AuthAuditEntry(
    long Id,
    DateTimeOffset AtUtc,
    string? Actor,
    string Action,
    string? Detail,
    string? IpAddress);
