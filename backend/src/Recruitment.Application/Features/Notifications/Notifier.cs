using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Notifications;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Notifications;

/// <summary>Bildirime eklenecek takvim daveti.</summary>
internal sealed record CalendarAttachment(string Content, CalendarMethod Method);

/// <summary>
/// Bildirim ve e-postayı tek yerden üretir. Event handler'lardan çağrılır; aynı olay iki kez
/// işlense de (outbox tekrarı) <c>dedupeKey</c> sayesinde tek bildirim ve tek e-posta oluşur.
/// </summary>
internal sealed class Notifier(
    IAppDbContext db,
    EmailComposer composer,
    INotificationPusher pusher,
    IAfterCommitQueue afterCommit,
    IOptions<RecruitmentOptions> options,
    TimeProvider clock)
{
    /// <summary>Verilen kullanıcılara bildirim gönderir; tercihine göre e-postayla da iletir.</summary>
    public async Task Notify(
        IEnumerable<Guid> recipientIds,
        NotificationType type,
        string title,
        string? body,
        string? link,
        string dedupeKey,
        CancellationToken ct,
        Func<Guid, CalendarAttachment?>? calendarFor = null)
    {
        var ids = recipientIds.Distinct().ToList();
        if (ids.Count == 0)
            return;

        var alreadyNotified = await db.Notifications
            .Where(n => ids.Contains(n.RecipientUserId) && n.DedupeKey == dedupeKey)
            .Select(n => n.RecipientUserId)
            .ToListAsync(ct);
        var users = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.IsActive && !alreadyNotified.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync(ct);
        var emailOff = await db.NotificationPreferences
            .Where(p => ids.Contains(p.UserId) && p.Type == type && !p.EmailEnabled)
            .Select(p => p.UserId)
            .ToListAsync(ct);

        var now = clock.GetUtcNow();
        var created = new List<Notification>();
        foreach (var user in users)
        {
            var notification = Notification.Create(user.Id, type, title, body, link, dedupeKey, now);
            db.Notifications.Add(notification);
            created.Add(notification);

            if (string.IsNullOrWhiteSpace(user.Email) || emailOff.Contains(user.Id))
                continue;

            var (subject, html) = await composer.Render(
                EmailTemplateCodes.Notification,
                new Dictionary<string, string?>
                {
                    ["AdSoyad"] = user.DisplayName,
                    ["Baslik"] = title,
                    ["Mesaj"] = body,
                    ["Baglanti"] = AbsoluteLink(link),
                },
                ct);
            var calendar = calendarFor?.Invoke(user.Id);
            db.OutgoingEmails.Add(OutgoingEmail.Create(
                user.Email, user.DisplayName, subject, html, $"{dedupeKey}:{user.Id}", now,
                calendarContent: calendar?.Content, calendarMethod: calendar?.Method));
        }

        await db.SaveChangesAsync(ct);

        // Anlık iletim commit'ten sonra ve en iyi çabayla yapılır; başarısız olursa bildirim yine
        // listede görünür.
        foreach (var notification in created)
        {
            var dto = notification.ToDto();
            afterCommit.Enqueue(token => pusher.Push(notification.RecipientUserId, dto, token));
        }
    }

    /// <summary>Adaya e-posta kuyruğa alır (adayın e-postası yoksa hiçbir şey yapmaz).</summary>
    public async Task EmailCandidate(
        Guid candidateId,
        string templateCode,
        IReadOnlyDictionary<string, string?> values,
        string dedupeKey,
        CancellationToken ct,
        CalendarAttachment? calendar = null)
    {
        var candidate = await db.Candidates.AsNoTracking()
            .Where(c => c.Id == candidateId)
            .Select(c => new { c.Email, c.FirstName, c.LastName })
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(candidate?.Email) || await db.OutgoingEmails.AnyAsync(e => e.DedupeKey == dedupeKey, ct))
            return;

        var (subject, html) = await composer.Render(templateCode, values, ct);
        db.OutgoingEmails.Add(OutgoingEmail.Create(
            candidate.Email, $"{candidate.FirstName} {candidate.LastName}", subject, html, dedupeKey, clock.GetUtcNow(),
            candidateId, calendar?.Content, calendar?.Method));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Verilen roldeki aktif kullanıcılar; birim verilirse yalnızca o birimdekiler.</summary>
    public Task<List<Guid>> UsersInRole(Role role, Guid? departmentId, CancellationToken ct) =>
        db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Roles.Any(r => r.Role == role) && (departmentId == null || u.DepartmentId == departmentId))
            .Select(u => u.Id)
            .ToListAsync(ct);

    public string AbsoluteLink(string? link) =>
        link is null ? options.Value.FrontendBaseUrl : $"{options.Value.FrontendBaseUrl.TrimEnd('/')}{link}";
}

internal static class NotificationMappings
{
    public static NotificationDto ToDto(this Notification n) =>
        new(n.Id, n.Type, n.Title, n.Body, n.Link, n.CreatedAt, n.IsRead);
}
