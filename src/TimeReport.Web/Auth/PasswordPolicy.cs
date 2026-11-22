using System.Globalization;

namespace TimeReport.Web.Auth;

public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 1024;

    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456789012",
        "password1234",
        "password12345",
        "qwertyuiop12",
        "letmein12345",
        "changeme1234",
        "administrator",
        "welcome12345",
    };

    public static string? Validate(string? password, string? username = null)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "Enter a password.";
        }

        var length = new StringInfo(password).LengthInTextElements;
        if (length < MinimumLength)
        {
            return $"The password must be at least {MinimumLength} characters.";
        }

        if (password.Length > MaximumLength)
        {
            return $"The password must be at most {MaximumLength} characters.";
        }

        if (!string.IsNullOrWhiteSpace(username) &&
            string.Equals(password, username, StringComparison.OrdinalIgnoreCase))
        {
            return "The password must not be the same as the username.";
        }

        if (CommonPasswords.Contains(password))
        {
            return "Choose a less common password.";
        }

        var unique = password.Distinct().Count();
        if (unique < 4)
        {
            return "Choose a password with more variety.";
        }

        return null;
    }
}
