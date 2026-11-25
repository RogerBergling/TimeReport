using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TimeReport.Web.Auth;

public sealed class PasswordService
{
    /// <summary>
    /// PBKDF2 work is linear in the supplied password length. The login form
    /// allows up to 100,000 characters, so cap what reaches the hasher.
    /// </summary>
    public const int MaxPasswordLength = 1024;

    private readonly PasswordHasher<AuthUser> _hasher;
    private readonly string _dummyHash;

    public PasswordService()
    {
        _hasher = new PasswordHasher<AuthUser>(
            Options.Create(new PasswordHasherOptions
            {
                CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
                IterationCount = 210_000,
            }));

        var dummy = new AuthUser
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = "dummy",
        };
        // Burn the same PBKDF2 work for unknown usernames so login timing does
        // not reveal whether an account exists.
        _dummyHash = _hasher.HashPassword(dummy, Guid.NewGuid().ToString("N"));
    }

    public string Hash(AuthUser user, string password)
    {
        if (password.Length > MaxPasswordLength)
        {
            throw new ArgumentException(
                $"Passwords longer than {MaxPasswordLength} characters are not accepted.",
                nameof(password));
        }
        return _hasher.HashPassword(user, password);
    }

    public PasswordVerificationResult Verify(AuthUser user, string password)
    {
        if (password.Length > MaxPasswordLength)
        {
            // Burn the same hashing work but never accept an oversized input.
            VerifyDummy(password[..MaxPasswordLength]);
            return PasswordVerificationResult.Failed;
        }
        return _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
    }

    public void VerifyDummy(string password)
    {
        var dummy = new AuthUser
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = "dummy",
        };
        var bounded = password.Length > MaxPasswordLength
            ? password[..MaxPasswordLength]
            : password;
        _hasher.VerifyHashedPassword(dummy, _dummyHash, bounded);
    }
}
