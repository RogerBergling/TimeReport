using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TimeReport.Web.Auth;

public sealed class PasswordService
{
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

    public string Hash(AuthUser user, string password) =>
        _hasher.HashPassword(user, password);

    public PasswordVerificationResult Verify(AuthUser user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password);

    public void VerifyDummy(string password)
    {
        var dummy = new AuthUser
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = "dummy",
        };
        _hasher.VerifyHashedPassword(dummy, _dummyHash, password);
    }
}
