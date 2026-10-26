using Recruitment.Domain.Notifications;

namespace Recruitment.Application.Abstractions;

public sealed record NotificationDto(
    Guid Id, NotificationType Type, string Title, string? Body, string? Link, DateTimeOffset CreatedAt, bool IsRead);

/// <summary>Yeni bildirimi kullanıcının açık oturumlarına anında iletir (SignalR).</summary>
public interface INotificationPusher
{
    Task Push(Guid userId, NotificationDto notification, CancellationToken ct);
}

public sealed record EmailEnvelope(
    string To, string? ToName, string Subject, string HtmlBody, string? CalendarContent, CalendarMethod? CalendarMethod);

public interface IEmailSender
{
    /// <summary>SMTP ayarları dolu mu? Değilse e-postalar gönderilmez, yalnızca kayda geçer.</summary>
    bool IsConfigured { get; }

    Task Send(EmailEnvelope email, CancellationToken ct);
}

public sealed record CalendarAttendee(string Name, string Email);

/// <summary>Takvim daveti (.ics) içeriği. Aynı mülakat için <see cref="Uid"/> sabit, <see cref="Sequence"/> artar.</summary>
public sealed record CalendarInvite(
    string Uid,
    int Sequence,
    CalendarMethod Method,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Summary,
    string? Description,
    string? Location,
    string? Url,
    IReadOnlyList<CalendarAttendee> Attendees);

public interface IIcsGenerator
{
    string Create(CalendarInvite invite);
}
