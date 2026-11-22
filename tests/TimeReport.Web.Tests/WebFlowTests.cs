using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using TimeReport.Web.Demo;

namespace TimeReport.Web.Tests;

public sealed partial class WebFlowTests
{
    private const string AdminPassword = "correct-horse-battery-staple";
    private const string UserPassword = "user-correct-horse-battery";

    [Fact]
    public async Task HealthAndSecurityHeadersArePresent()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClientWithoutRedirects();

        var response = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var csp));
        Assert.Contains("default-src 'self'", string.Join(" ", csp));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task DemoModeSeedsGuestDataShowsTotpAndProtectsGuestCredentials()
    {
        using var factory = new TestWebApplicationFactory(demoMode: true);
        using var client = factory.CreateClientWithoutRedirects();

        var loginPage = await client.GetStringAsync("/login");
        Assert.Contains("Guest access", loginPage);
        Assert.Contains(DemoInfo.GuestUsernameValue, loginPage);
        Assert.Contains(DemoInfo.GuestTotpSecret, loginPage);
        Assert.Contains(DemoInfo.GuestRecoveryCodeValue, loginPage);
        Assert.Contains("data-demo-otp", loginPage);

        var otp = await client.GetAsync("/demo/otp");
        Assert.Equal(HttpStatusCode.OK, otp.StatusCode);
        Assert.Contains("\"code\"", await otp.Content.ReadAsStringAsync());

        var login = await PostFormAsync(
            client,
            "/login",
            TestHtml.AntiforgeryToken(loginPage),
            new()
            {
                ["Username"] = DemoInfo.GuestUsernameValue,
                ["Password"] = DemoInfo.GuestPasswordValue,
            });
        AssertRedirect(login, "/login/mfa");

        var mfaPage = await client.GetStringAsync("/login/mfa");
        Assert.Contains("Guest access", mfaPage);
        var mfa = await PostFormAsync(
            client,
            "/login/mfa",
            TestHtml.AntiforgeryToken(mfaPage),
            new() { ["Code"] = TotpTestHelper.Code(DemoInfo.GuestTotpSecret) });
        AssertRedirect(mfa, "/");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        var projects = await client.GetStringAsync("/projects");
        Assert.Contains("Customer Portal", projects);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/users")).StatusCode);

        var passwordPage = await client.GetStringAsync("/account/password");
        Assert.Contains("Demo guest credentials are fixed", passwordPage);
        var blocked = await PostFormAsync(
            client,
            "/account/password",
            TestHtml.AntiforgeryToken(passwordPage),
            new()
            {
                ["CurrentPassword"] = DemoInfo.GuestPasswordValue,
                ["NewPassword"] = "new-demo-password-123",
                ["ConfirmPassword"] = "new-demo-password-123",
            });
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
        Assert.Contains("fixed credentials", await blocked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StateChangingPostWithoutAntiforgeryTokenIsRejected()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClientWithoutRedirects();

        var response = await client.PostAsync(
            "/setup",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["SetupToken"] = TestWebApplicationFactory.SetupToken,
                ["Username"] = "admin",
                ["Password"] = AdminPassword,
                ["ConfirmPassword"] = AdminPassword,
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FirstRunSetupTotpRecoveryAndTimesheetFlow()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClientWithoutRedirects();

        var setupPage = await client.GetStringAsync("/setup");
        Assert.Contains("Create the first administrator", setupPage);

        var wrong = await PostFormAsync(
            client,
            "/setup",
            TestHtml.AntiforgeryToken(setupPage),
            new()
            {
                ["SetupToken"] = "wrong-token-1234567890",
                ["Username"] = "admin",
                ["DisplayName"] = "Admin",
                ["Password"] = AdminPassword,
                ["ConfirmPassword"] = AdminPassword,
            });
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);

        var (secret, recoveryCodes) = await CompleteSetupAsync(client);
        Assert.Equal(10, recoveryCodes.Count);
        Assert.Equal(32, secret.Length);

        var home = await client.GetStringAsync("/");
        Assert.Contains("Your week", home);

        await CreateProjectAsync(client, "Test project");
        await CreateEntryAsync(client, "2026-11-20");
        home = await client.GetStringAsync("/");
        Assert.Contains("Test project", home);

        var totals = await client.GetStringAsync("/totals?period=week&anchor=2026-11-20");
        Assert.Contains("Worked", totals);

        var export = await client.GetAsync("/export/download?start=2026-11-01&end=2026-11-30");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            export.Content.Headers.ContentType?.MediaType);
        var bytes = await export.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public async Task PasswordLoginRequiresTotpAndSupportsRecoveryCodes()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClientWithoutRedirects();
        var (secret, recoveryCodes) = await CompleteSetupAsync(client);

        await LogoutAsync(client);

        var loginPage = await client.GetStringAsync("/login");
        var login = await PostFormAsync(
            client,
            "/login",
            TestHtml.AntiforgeryToken(loginPage),
            new()
            {
                ["Username"] = "admin",
                ["Password"] = AdminPassword,
            });
        AssertRedirect(login, "/login/mfa");

        var mfaPage = await client.GetStringAsync("/login/mfa");
        var wrongMfa = await PostFormAsync(
            client,
            "/login/mfa",
            TestHtml.AntiforgeryToken(mfaPage),
            new() { ["Code"] = "000000" });
        Assert.Equal(HttpStatusCode.OK, wrongMfa.StatusCode);

        mfaPage = await client.GetStringAsync("/login/mfa");
        var correctMfa = await PostFormAsync(
            client,
            "/login/mfa",
            TestHtml.AntiforgeryToken(mfaPage),
            // The setup flow consumed the current TOTP step. Use the next
            // step so the replay protection is exercised without waiting 30s.
            new() { ["Code"] = TotpTestHelper.Code(secret, DateTimeOffset.UtcNow.AddSeconds(30)) });
        AssertRedirect(correctMfa, "/");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        await LogoutAsync(client);
        loginPage = await client.GetStringAsync("/login");
        await PostFormAsync(
            client,
            "/login",
            TestHtml.AntiforgeryToken(loginPage),
            new()
            {
                ["Username"] = "admin",
                ["Password"] = AdminPassword,
            });
        var recoveryPage = await client.GetStringAsync("/login/recovery");
        var recovery = await PostFormAsync(
            client,
            "/login/recovery",
            TestHtml.AntiforgeryToken(recoveryPage),
            new() { ["Code"] = recoveryCodes[0] });
        AssertRedirect(recovery, "/");
    }

    [Fact]
    public async Task MultiuserDataIsIsolatedAndAdminPagesAreProtected()
    {
        using var factory = new TestWebApplicationFactory();
        using var admin = factory.CreateClientWithoutRedirects();
        await CompleteSetupAsync(admin);
        await CreateProjectAsync(admin, "Admin project");

        var usersPage = await admin.GetStringAsync("/admin/users");
        var create = await PostFormAsync(
            admin,
            "/admin/users?handler=Create",
            TestHtml.AntiforgeryToken(usersPage),
            new()
            {
                ["NewUsername"] = "alice",
                ["NewDisplayName"] = "Alice",
                ["NewPassword"] = string.Empty,
                ["NewIsAdmin"] = "false",
            });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var createHtml = await create.Content.ReadAsStringAsync();
        var temporaryPassword = TemporaryPasswordPattern().Match(createHtml).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(temporaryPassword));

        using var alice = factory.CreateClientWithoutRedirects();
        var loginPage = await alice.GetStringAsync("/login");
        var login = await PostFormAsync(
            alice,
            "/login",
            TestHtml.AntiforgeryToken(loginPage),
            new()
            {
                ["Username"] = "alice",
                ["Password"] = temporaryPassword,
            });
        AssertRedirect(login, "/account/password");

        var passwordPage = await alice.GetStringAsync("/account/password");
        var password = await PostFormAsync(
            alice,
            "/account/password",
            TestHtml.AntiforgeryToken(passwordPage),
            new()
            {
                ["CurrentPassword"] = temporaryPassword,
                ["NewPassword"] = UserPassword,
                ["ConfirmPassword"] = UserPassword,
            });
        AssertRedirect(password, "/account/totp");

        var totpPage = await alice.GetStringAsync("/account/totp");
        var secret = TestHtml.TotpSecret(totpPage);
        var totp = await PostFormAsync(
            alice,
            "/account/totp",
            TestHtml.AntiforgeryToken(totpPage),
            new()
            {
                ["CurrentPassword"] = UserPassword,
                ["Code"] = TotpTestHelper.Code(secret),
            });
        AssertRedirect(totp, "/account/recovery-codes");

        var recoveryPage = await alice.GetStringAsync("/account/recovery-codes");
        var recovery = await PostFormAsync(
            alice,
            "/account/recovery-codes",
            TestHtml.AntiforgeryToken(recoveryPage),
            new() { ["CurrentPassword"] = UserPassword });
        Assert.Equal(HttpStatusCode.OK, recovery.StatusCode);

        await CreateProjectAsync(alice, "Alice private");
        await CreateEntryAsync(alice, "2026-11-20");

        var adminHome = await admin.GetStringAsync("/");
        var aliceHome = await alice.GetStringAsync("/");
        Assert.DoesNotContain("Alice private", adminHome);
        Assert.DoesNotContain("Admin project", aliceHome);
        Assert.Contains("Alice private", aliceHome);

        var denied = await alice.GetAsync("/admin/users");
        AssertRedirect(denied, "/denied");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/admin/users")).StatusCode);
    }

    private static async Task<(string Secret, IReadOnlyList<string> RecoveryCodes)> CompleteSetupAsync(
        HttpClient client)
    {
        var setupPage = await client.GetStringAsync("/setup");
        var setup = await PostFormAsync(
            client,
            "/setup",
            TestHtml.AntiforgeryToken(setupPage),
            new()
            {
                ["SetupToken"] = "test-setup-token-1234567890",
                ["Username"] = "admin",
                ["DisplayName"] = "Admin",
                ["Password"] = AdminPassword,
                ["ConfirmPassword"] = AdminPassword,
            });
        AssertRedirect(setup, "/account/totp");

        var totpPage = await client.GetStringAsync("/account/totp");
        var secret = TestHtml.TotpSecret(totpPage);
        var totp = await PostFormAsync(
            client,
            "/account/totp",
            TestHtml.AntiforgeryToken(totpPage),
            new()
            {
                ["CurrentPassword"] = AdminPassword,
                ["Code"] = TotpTestHelper.Code(secret),
            });
        AssertRedirect(totp, "/account/recovery-codes");

        var recoveryPage = await client.GetStringAsync("/account/recovery-codes");
        var recovery = await PostFormAsync(
            client,
            "/account/recovery-codes",
            TestHtml.AntiforgeryToken(recoveryPage),
            new() { ["CurrentPassword"] = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, recovery.StatusCode);
        var html = await recovery.Content.ReadAsStringAsync();
        var codes = RecoveryCodePattern().Matches(html)
            .Select(match => match.Groups[1].Value)
            .ToList();
        return (secret, codes);
    }

    private static async Task CreateProjectAsync(HttpClient client, string name)
    {
        var pageResponse = await client.GetAsync("/projects");
        var page = await pageResponse.Content.ReadAsStringAsync();
        Assert.True(pageResponse.IsSuccessStatusCode, page);
        var response = await PostFormAsync(
            client,
            "/projects?handler=Create",
            TestHtml.AntiforgeryToken(page),
            new() { ["name"] = name });
        AssertRedirect(response, "/projects");
    }

    private static async Task CreateEntryAsync(HttpClient client, string date)
    {
        var page = await client.GetStringAsync($"/entries/edit?date={date}");
        var typeId = Regex.Match(page, "<option value=\"(\\d+)\" data-category=\"work\"").Groups[1].Value;
        var projectId = Regex.Match(
            page,
            "<select[^>]*id=\"ProjectId\"[^>]*>.*?<option value=\"(\\d+)\"",
            RegexOptions.Singleline).Groups[1].Value;
        var timeZone = Regex.Match(
            page,
            "<select[^>]*id=\"TimeZoneId\"[^>]*>.*?<option value=\"([^\"]+)\"",
            RegexOptions.Singleline).Groups[1].Value;

        var response = await PostFormAsync(
            client,
            "/entries/edit",
            TestHtml.AntiforgeryToken(page),
            new()
            {
                ["Date"] = date,
                ["TypeId"] = typeId,
                ["ProjectId"] = projectId,
                ["AwayReason"] = string.Empty,
                ["Start"] = "13:00",
                ["End"] = "17:00",
                ["TimeZoneId"] = timeZone,
            });
        AssertRedirect(response, "/");
    }

    private static async Task LogoutAsync(HttpClient client)
    {
        var page = await client.GetStringAsync("/");
        var response = await PostFormAsync(
            client,
            "/logout",
            TestHtml.AntiforgeryToken(page),
            new());
        AssertRedirect(response, "/login");
    }

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string url,
        string antiforgeryToken,
        Dictionary<string, string> values)
    {
        values["__RequestVerificationToken"] = antiforgeryToken;
        return await client.PostAsync(url, new FormUrlEncodedContent(values));
    }

    private static void AssertRedirect(HttpResponseMessage response, string pathPrefix)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = response.Headers.Location!;
        var path = location.IsAbsoluteUri ? location.AbsolutePath : location.ToString();
        Assert.StartsWith(pathPrefix, path);
    }

    [GeneratedRegex("id=\"temporary-password\">([^<]+)</span>")]
    private static partial Regex TemporaryPasswordPattern();

    [GeneratedRegex("class=\"recovery-code\">([^<]+)</span>")]
    private static partial Regex RecoveryCodePattern();
}
