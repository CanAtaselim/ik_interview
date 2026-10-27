using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Users.Preferences;

namespace Recruitment.Api.Controllers;

/// <summary>Giriş yapan kullanıcının kişisel arayüz tercihleri (ör. dashboard kart sırası). Herkes yalnızca kendi kaydına erişir.</summary>
[ApiController]
[Route("api/v1/me/preferences")]
public sealed class PreferencesController(IDispatcher dispatcher) : ControllerBase
{
    public sealed record SaveRequest(JsonElement Value);

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, CancellationToken ct) =>
        (await dispatcher.Query(new GetUserPreferenceQuery(key), ct)).ToActionResult(this);

    [HttpPut("{key}")]
    public async Task<IActionResult> Save(string key, SaveRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new SaveUserPreferenceCommand(key, request.Value), ct)).ToActionResult(this);

    [HttpDelete("{key}")]
    public async Task<IActionResult> Reset(string key, CancellationToken ct) =>
        (await dispatcher.Send(new ResetUserPreferenceCommand(key), ct)).ToActionResult(this);
}
