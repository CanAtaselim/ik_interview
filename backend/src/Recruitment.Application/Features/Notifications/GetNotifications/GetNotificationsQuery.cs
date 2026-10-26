using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Notifications.GetNotifications;

/// <summary>Kullanıcının bildirimleri, en yeni başta.</summary>
public sealed record GetNotificationsQuery(bool UnreadOnly, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<NotificationPageDto>;

/// <param name="UnreadCount">Okunmamış bildirimlerin toplamı (zil sayacı).</param>
public sealed record NotificationPageDto(
    IReadOnlyList<NotificationDto> Items, int TotalCount, int Page, int PageSize, int UnreadCount);

internal sealed class GetNotificationsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetNotificationsQuery, NotificationPageDto>
{
    public async Task<Result<NotificationPageDto>> Handle(GetNotificationsQuery query, CancellationToken ct)
    {
        var mine = db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == currentUser.UserId);
        var filtered = query.UnreadOnly ? mine.Where(n => n.ReadAt == null) : mine;

        var page = await filtered
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt != null))
            .ToPagedResult(query.Page, query.PageSize, ct);
        var unread = await mine.CountAsync(n => n.ReadAt == null, ct);

        return new NotificationPageDto(page.Items, page.TotalCount, page.Page, page.PageSize, unread);
    }
}
