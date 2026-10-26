using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Auth;
using Recruitment.Application.Features.Auth.GetCurrentUser;
using Recruitment.Application.Features.Auth.Login;
using Recruitment.Application.Features.Auth.Logout;
using Recruitment.Application.Features.Auth.RefreshSession;

namespace Recruitment.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IDispatcher dispatcher, IHostEnvironment environment) : ControllerBase
{
    public sealed record LoginRequest(string Username, string Password);

    public sealed record SessionResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserDto User);

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    // Geliştirmede http://localhost üzerinden çalışabilmek için; diğer ortamlarda cookie her zaman Secure.
    private bool UseSecureCookie => Request.IsHttps || !environment.IsDevelopment();

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await dispatcher.Send(new LoginCommand(request.Username, request.Password, ClientIp), ct);
        return ToSessionResult(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        // Başarısızlıkta cookie silinmez: aynı anda yenileyen başka bir sekmenin az önce aldığı
        // yeni cookie'yi ezebilirdi.
        var result = await dispatcher.Send(new RefreshSessionCommand(RefreshTokenCookie.Read(Request), ClientIp), ct);
        return ToSessionResult(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var result = await dispatcher.Send(new LogoutCommand(RefreshTokenCookie.Read(Request)), ct);
        RefreshTokenCookie.Delete(Response, UseSecureCookie);
        return result.ToActionResult(this);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await dispatcher.Query(new GetCurrentUserQuery(), ct);
        return result.ToActionResult(this);
    }

    private IActionResult ToSessionResult(Result<AuthSession> result)
    {
        if (result.IsFailure)
            return result.Error.ToProblemResult(this);

        var session = result.Value;
        RefreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAt, UseSecureCookie);

        return Ok(new SessionResponse(session.AccessToken, session.AccessTokenExpiresAt, session.User));
    }
}
