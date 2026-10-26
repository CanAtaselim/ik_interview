using Microsoft.AspNetCore.Mvc;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Notifications.GetNotifications;
using Recruitment.Application.Features.Notifications.MarkNotificationsRead;
using Recruitment.Application.Features.Notifications.Preferences;

namespace Recruitment.Api.Controllers;

/// <summary>Giriş yapan kullanıcının bildirimleri ve e-posta tercihleri.</summary>
[ApiController]
[Route("api/v1/me")]
public sealed class NotificationsController(IDispatcher dispatcher) : ControllerBase
{
    public sealed record PreferencesRequest(IReadOnlyList<NotificationPreferenceDto> Preferences);

    [HttpGet("notifications")]
    public async Task<IActionResult> GetList(
        CancellationToken ct,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize) =>
        (await dispatcher.Query(new GetNotificationsQuery(unreadOnly, page, pageSize), ct)).ToActionResult(this);

    [HttpPost("notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new MarkNotificationsReadCommand(id), ct)).ToActionResult(this);

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct) =>
        (await dispatcher.Send(new MarkNotificationsReadCommand(null), ct)).ToActionResult(this);

    [HttpGet("notification-preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct) =>
        (await dispatcher.Query(new GetNotificationPreferencesQuery(), ct)).ToActionResult(this);

    [HttpPut("notification-preferences")]
    public async Task<IActionResult> UpdatePreferences(PreferencesRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new UpdateNotificationPreferencesCommand(request.Preferences), ct)).ToActionResult(this);
}
