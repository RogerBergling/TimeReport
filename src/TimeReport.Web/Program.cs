using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TimeReport;
using TimeReport.Web.Auth;
using TimeReport.Web.Configuration;
using TimeReport.Web.Demo;
using TimeReport.Web.Security;
using TimeReport.Web.Services;

if (args.Contains("--version", StringComparer.Ordinal))
{
    Console.WriteLine($"TimeReport Web {AppInfo.Version} (build {AppInfo.BuildNumber})");
    return;
}

if (args.Contains("--help", StringComparer.Ordinal) ||
    args.Contains("-h", StringComparer.Ordinal))
{
    Console.WriteLine(
        """
        TimeReport Web

        Environment:
          TIMEREPORT_DATA_DIR             Data directory (default /data in a container).
          TIMEREPORT_MASTER_KEY           Base64 32-byte master key.
          TIMEREPORT_MASTER_KEY_FILE      File containing the master key.
          TIMEREPORT_SETUP_TOKEN          Optional first-run setup token.
          TIMEREPORT_SETUP_TOKEN_FILE     File containing the setup token.
          TIMEREPORT_COOKIE_SECURE        true/false (default true).
          TIMEREPORT_TRUSTED_PROXIES      Comma-separated proxy IPs/CIDRs.
          TIMEREPORT_ALLOWED_HOSTS        Allowed Host header values.
          TIMEREPORT_SESSION_MINUTES      Idle session lifetime (default 30).
          TIMEREPORT_SESSION_ABSOLUTE_HOURS  Absolute session lifetime (default 12).
          TIMEREPORT_MFA_MINUTES          Pending MFA lifetime (default 5).

        Commands:
          --reset-admin-mfa <username>    Break-glass: clear TOTP and recovery codes
                                          for an administrator, revoke sessions and
                                          exit. Stop the service first.
          --version                       Print the version and exit.
          -h, --help                      Print this help and exit.
        """);
    return;
}

var resetIndex = Array.IndexOf(args, "--reset-admin-mfa");
if (resetIndex >= 0)
{
    if (resetIndex + 1 >= args.Length)
    {
        Console.Error.WriteLine("Usage: TimeReport.Web --reset-admin-mfa <username>");
        Environment.ExitCode = 2;
        return;
    }

    try
    {
        var cliConfiguration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var cliOptions = WebOptions.Load(cliConfiguration);
        var cliMasterKey = MasterKey.Load(cliOptions);
        WebPaths.EnsurePrivateDirectory(cliOptions.DataDir);
        var cliDatabase = new AuthDatabase(
            Path.Combine(cliOptions.DataDir, "auth.db"),
            cliMasterKey.DeriveBase64("timereport-auth-db-v1"));
        cliDatabase.Initialize();
        var user = cliDatabase.GetUserByUsername(args[resetIndex + 1]);
        if (user is null || !user.IsAdmin)
        {
            Console.Error.WriteLine("No administrator with that username exists.");
            Environment.ExitCode = 1;
            return;
        }

        var stamp = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        cliDatabase.ResetTotp(user.Id, stamp);
        cliDatabase.Audit("cli", "admin.mfa_reset", user.Username, null);
        Console.WriteLine(
            $"MFA reset for {user.Username}. Existing sessions are revoked. " +
            "The user must sign in with their password and enroll a new authenticator.");
        return;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Could not reset MFA: {exception.Message}");
        Environment.ExitCode = 1;
        return;
    }
}

var builder = WebApplication.CreateBuilder(args);
var options = WebOptions.Load(builder.Configuration);
var masterKey = MasterKey.Load(options);

if (options.DemoMode)
{
    Console.WriteLine(
        "*** DEMO MODE: fake data, public guest credentials and a data reset every " +
        $"{options.DemoResetHours} hour(s). Never use this mode for real data. ***");
    DemoDataStore.ResetForDemo(options.DataDir);
}

WebPaths.EnsurePrivateDirectory(options.DataDir);
WebPaths.EnsurePrivateDirectory(Path.Combine(options.DataDir, "users"));

var authDatabase = new AuthDatabase(
    Path.Combine(options.DataDir, "auth.db"),
    masterKey.DeriveBase64("timereport-auth-db-v1"));
authDatabase.Initialize();

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(masterKey);
builder.Services.AddSingleton(authDatabase);
builder.Services.AddSingleton<DemoInfo>();
builder.Services.AddScoped<DemoSeeder>();
if (options.DemoMode)
{
    builder.Services.AddHostedService<DemoResetService>();
}
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<TotpService>();
builder.Services.AddSingleton<RecoveryCodeService>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton<SetupTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthCookieService>();
builder.Services.AddScoped<AuthCookieEvents>();
builder.Services.AddScoped<UserDataStore>();
builder.Services.AddHttpContextAccessor();

builder.Configuration["AllowedHosts"] = options.AllowedHosts;
builder.WebHost.UseSetting("AllowedHosts", options.AllowedHosts);
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize = 1024 * 1024;
});

builder.Services.Configure<FormOptions>(formOptions =>
{
    formOptions.ValueLengthLimit = 100_000;
    formOptions.MultipartBodyLengthLimit = 1024 * 1024;
    formOptions.ValueCountLimit = 1_000;
    formOptions.KeyLengthLimit = 2_048;
});

builder.Services.Configure<ForwardedHeadersOptions>(
    forwarded => ForwardedHeadersConfig.Configure(forwarded, options.TrustedProxies));

// Cookies and antiforgery tokens are intentionally ephemeral. A container
// restart signs users out. The key ring lives in the container's temporary
// filesystem, never on the persistent data volume. TOTP secrets and account
// state live in the encrypted auth database.
var dataProtectionDirectory = Path.Combine(Path.GetTempPath(), "timereport-dp-keys");
WebPaths.EnsurePrivateDirectory(dataProtectionDirectory);
builder.Services
    .AddDataProtection()
    .SetApplicationName("TimeReport")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionDirectory));

var cookieSecurePolicy = options.CookieSecure
    ? CookieSecurePolicy.Always
    : CookieSecurePolicy.SameAsRequest;

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(cookie =>
    {
        cookie.Cookie.Name = WebCookieNames.Auth(options);
        cookie.Cookie.HttpOnly = true;
        cookie.Cookie.SameSite = SameSiteMode.Strict;
        cookie.Cookie.SecurePolicy = cookieSecurePolicy;
        cookie.Cookie.Path = "/";
        cookie.Cookie.IsEssential = true;
        cookie.ExpireTimeSpan = TimeSpan.FromMinutes(options.SessionMinutes);
        cookie.SlidingExpiration = true;
        cookie.LoginPath = "/login";
        cookie.LogoutPath = "/logout";
        cookie.AccessDeniedPath = "/denied";
        cookie.ReturnUrlParameter = "returnUrl";
        cookie.EventsType = typeof(AuthCookieEvents);
    });

builder.Services.Configure<CookieTempDataProviderOptions>(tempData =>
{
    tempData.Cookie.Name = options.CookieSecure ? "__Host-timereport.temp" : "timereport.temp";
    tempData.Cookie.HttpOnly = true;
    tempData.Cookie.SameSite = SameSiteMode.Strict;
    tempData.Cookie.SecurePolicy = cookieSecurePolicy;
    tempData.Cookie.Path = "/";
    tempData.Cookie.IsEssential = true;
});

builder.Services.AddAntiforgery(antiforgery =>
{
    antiforgery.Cookie.Name = WebCookieNames.Antiforgery(options);
    antiforgery.Cookie.HttpOnly = true;
    antiforgery.Cookie.SameSite = SameSiteMode.Strict;
    antiforgery.Cookie.SecurePolicy = cookieSecurePolicy;
    antiforgery.Cookie.Path = "/";
    antiforgery.Cookie.IsEssential = true;
    antiforgery.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddAuthorization(authorization =>
{
    authorization.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    authorization.AddPolicy(
        "Admin",
        policy => policy.RequireClaim("is_admin", "true"));
});

builder.Services.Configure<RouteOptions>(route =>
{
    route.LowercaseUrls = true;
});
builder.Services.AddRazorPages();

var app = builder.Build();

if (options.DemoMode)
{
    using var demoScope = app.Services.CreateScope();
    demoScope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedIfNeeded();
}

app.Services.GetRequiredService<SetupTokenService>().Initialize();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseStatusCodePagesWithReExecute("/error", "?code={0}");
app.UseForwardedHeaders();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "public,max-age=604800";
    },
});

app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<LocalizationMiddleware>();
app.UseMiddleware<TotpGateMiddleware>();
app.UseAuthorization();

app.MapRazorPages();

app.MapGet(
        "/healthz",
        () => Results.Json(new
        {
            ok = true,
            version = AppInfo.Version,
            build = AppInfo.BuildNumber,
        }))
    .AllowAnonymous();

app.MapGet(
        "/readyz",
        (AuthDatabase database) =>
        {
            try
            {
                database.Ping();
                return Results.Json(new { ok = true });
            }
            catch (Exception)
            {
                return Results.Json(new { ok = false }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
    .AllowAnonymous();

if (options.DemoMode)
{
    app.MapGet(
            "/demo/otp",
            (DemoInfo demo, TotpService totp) =>
            {
                var now = DateTimeOffset.UtcNow;
                var code = totp.GenerateCode(DemoInfo.GuestTotpSecret, now);
                var secondsRemaining = (int)(30 - (now.ToUnixTimeSeconds() % 30));
                return Results.Json(new { code, secondsRemaining });
            })
        .AllowAnonymous();
}

app.Run();

public partial class Program;
