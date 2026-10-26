using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Users;
using Recruitment.Infrastructure.Persistence;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Auth;

public sealed class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string CookieName = "recruitment_refresh";

    private readonly HttpClient _client = factory.CreateApiClient();

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAccessTokenAndHttpOnlyRefreshCookie()
    {
        var username = factory.Ldap.AddUser(Role.HrSpecialist, Role.TeamLead);

        var response = await Login($@"SIRKET\{username.ToUpperInvariant()}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("user").GetProperty("username").GetString().ShouldBe(username);
        body.GetProperty("user").GetProperty("roles").EnumerateArray().Select(r => r.GetString())
            .ShouldBe(["HrSpecialist", "TeamLead"], ignoreOrder: true);
        body.ToString().ShouldNotContain("refreshToken", Case.Insensitive);

        var cookie = SetCookieHeader(response).ToLowerInvariant();
        cookie.ShouldContain("httponly");
        cookie.ShouldContain("secure");
        cookie.ShouldContain("samesite=strict");
        cookie.ShouldContain("path=/api/v1/auth");
    }

    [Fact]
    public async Task Login_WithWrongPasswordOrUnknownUser_ReturnsSame401()
    {
        var username = factory.Ldap.AddUser(Role.HrManager);

        var wrongPassword = await Login(username, "yanlis-sifre");
        var unknownUser = await Login("boyle-biri-yok", "yanlis-sifre");

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownUser.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Detail(wrongPassword)).ShouldBe("Kullanıcı adı veya şifre hatalı.");
        (await Detail(unknownUser)).ShouldBe("Kullanıcı adı veya şifre hatalı.");
        SetCookieHeader(wrongPassword).ShouldBeEmpty();
    }

    [Fact]
    public async Task Login_WithEmptyFields_Returns400WithTurkishFieldErrors()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.GetProperty("username")[0].GetString().ShouldBe("Kullanıcı adınızı girmelisiniz.");
        errors.GetProperty("password")[0].GetString().ShouldBe("Şifrenizi girmelisiniz.");
    }

    [Fact]
    public async Task Login_WhenUserHasNoMappedRole_Returns403()
    {
        var username = factory.Ldap.AddUser();

        var response = await Login(username);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        SetCookieHeader(response).ShouldBeEmpty();
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_LocksUsernameOutEvenWithCorrectPassword()
    {
        var username = factory.Ldap.AddUser(Role.HrManager);

        for (var attempt = 0; attempt < 5; attempt++)
            (await Login(username, "yanlis-sifre")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var response = await Login(username);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_ForNewUser_CreatesUserWithAuditTrail()
    {
        var username = factory.Ldap.AddUser(Role.Executive);

        await Login(username);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Username == username);
        user.CreatedBy.ShouldBe("system");
        user.LastLoginAt.ShouldNotBeNull();
        user.HasRole(Role.Executive).ShouldBeTrue();

        var audit = await db.AuditLogs.SingleAsync(a => a.EntityId == user.Id && a.Action == AuditAction.Created);
        audit.EntityType.ShouldBe(nameof(User));
        audit.NewValues.ShouldNotBeNull();
        audit.NewValues.ShouldContain(username);
        (await db.AuditLogs.AnyAsync(a => a.EntityType == nameof(RefreshToken))).ShouldBeFalse();
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Detail(response)).ShouldBe("Bu işlem için giriş yapmalısınız.");
    }

    [Fact]
    public async Task Me_WithAccessToken_ReturnsCurrentUser()
    {
        var username = factory.Ldap.AddUser(Role.TeamLead);
        var login = await Login(username);

        var response = await Send(HttpMethod.Get, "/api/v1/auth/me", accessToken: await AccessToken(login));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("username").GetString().ShouldBe(username);
        body.GetProperty("roles")[0].GetString().ShouldBe("TeamLead");
    }

    [Fact]
    public async Task Me_WithTamperedToken_Returns401()
    {
        var login = await Login(factory.Ldap.AddUser(Role.TeamLead));
        var token = await AccessToken(login);
        var tampered = token[..^4] + (token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        var response = await Send(HttpMethod.Get, "/api/v1/auth/me", accessToken: tampered);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var response = await Send(HttpMethod.Post, "/api/v1/auth/refresh");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithValidCookie_RotatesRefreshToken()
    {
        var login = await Login(factory.Ldap.AddUser(Role.HrManager));
        var original = RefreshCookie(login);

        var refreshed = await Refresh(original);

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotated = RefreshCookie(refreshed);
        rotated.ShouldNotBe(original);
        (await Refresh(rotated)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithJustRotatedToken_IsRejectedButKeepsNewSessionAlive()
    {
        var login = await Login(factory.Ldap.AddUser(Role.HrManager));
        var original = RefreshCookie(login);
        var rotated = RefreshCookie(await Refresh(original));

        var replay = await Refresh(original);

        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Refresh(rotated)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithRevokedTokenAfterGracePeriod_ClosesAllSessionsOfUser()
    {
        var username = factory.Ldap.AddUser(Role.HrManager);
        var original = RefreshCookie(await Login(username));
        var rotated = RefreshCookie(await Refresh(original));
        var otherDevice = RefreshCookie(await Login(username));
        await BackdateRevocations(username);

        var replay = await Refresh(original);

        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Refresh(rotated)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Refresh(otherDevice)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WhenRolesChangedInDirectory_IssuesTokenWithNewRoles()
    {
        var username = factory.Ldap.AddUser(Role.HrSpecialist);
        var cookie = RefreshCookie(await Login(username));
        factory.Ldap.SetRoles(username, Role.HrManager);

        var response = await Refresh(cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("user").GetProperty("roles").EnumerateArray().Select(r => r.GetString())
            .ShouldBe(["HrManager"]);
    }

    [Fact]
    public async Task Refresh_WhenAllRolesRemovedInDirectory_Returns403AndEndsSession()
    {
        var username = factory.Ldap.AddUser(Role.HrSpecialist);
        var cookie = RefreshCookie(await Login(username));
        factory.Ldap.SetRoles(username);

        var response = await Refresh(cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        factory.Ldap.SetRoles(username, Role.HrSpecialist);
        (await Refresh(cookie)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WhenUserDisabledInDirectory_Returns401()
    {
        var username = factory.Ldap.AddUser(Role.HrSpecialist);
        var cookie = RefreshCookie(await Login(username));
        factory.Ldap.Disable(username);

        var response = await Refresh(cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithActiveSession_RevokesRefreshTokenAndClearsCookie()
    {
        var login = await Login(factory.Ldap.AddUser(Role.HrManager));
        var cookie = RefreshCookie(login);

        var response = await Send(
            HttpMethod.Post, "/api/v1/auth/logout", accessToken: await AccessToken(login), refreshCookie: cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        SetCookieHeader(response).ShouldContain($"{CookieName}=;");
        (await Refresh(cookie)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> Login(string username, string password = TestLdapService.Password) =>
        _client.PostAsJsonAsync("/api/v1/auth/login", new { username, password });

    private Task<HttpResponseMessage> Refresh(string refreshCookie) =>
        Send(HttpMethod.Post, "/api/v1/auth/refresh", refreshCookie: refreshCookie);

    private Task<HttpResponseMessage> Send(
        HttpMethod method, string url, string? accessToken = null, string? refreshCookie = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (refreshCookie is not null)
            request.Headers.Add("Cookie", $"{CookieName}={refreshCookie}");

        return _client.SendAsync(request);
    }

    // İptal zamanlarını geçmişe çeker: rotasyon sonrası 30 sn'lik hoşgörü penceresi dolmuş gibi olur.
    private async Task BackdateRevocations(string username)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlAsync($"""
            update refresh_tokens set revoked_at = revoked_at - interval '5 minutes'
            where revoked_at is not null
              and user_id = (select id from users where username = {username})
            """);
    }

    private static async Task<string> AccessToken(HttpResponseMessage login) =>
        (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

    private static async Task<string?> Detail(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();

    private static string SetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? string.Join("\n", values) : string.Empty;

    private static string RefreshCookie(HttpResponseMessage response)
    {
        var header = SetCookieHeader(response);
        header.ShouldStartWith($"{CookieName}=");

        return header[(CookieName.Length + 1)..header.IndexOf(';')];
    }
}
