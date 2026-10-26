using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Recruitment.Application.Abstractions;

namespace Recruitment.Api.Hubs;

/// <summary>
/// Anlık bildirim kanalı. İstemci yalnızca dinler; bildirimler sunucudan kullanıcının kimliğine
/// göre (<see cref="SubjectUserIdProvider"/>) iletilir.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public const string Path = "/hubs/notifications";
    public const string NotificationMethod = "notification";
}

/// <summary>SignalR kullanıcı kimliği olarak token'daki "sub" (kullanıcı kimliği) kullanılır.</summary>
internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst(AuthClaims.Subject)?.Value;
}

internal sealed class SignalRNotificationPusher(IHubContext<NotificationHub> hub) : INotificationPusher
{
    public Task Push(Guid userId, NotificationDto notification, CancellationToken ct) =>
        hub.Clients.User(userId.ToString()).SendAsync(NotificationHub.NotificationMethod, notification, ct);
}
