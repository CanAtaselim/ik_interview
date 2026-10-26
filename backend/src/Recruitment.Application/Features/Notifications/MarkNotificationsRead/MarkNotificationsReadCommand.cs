using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Notifications.MarkNotificationsRead;

/// <summary>Bildirimleri okundu işaretler; <see cref="Id"/> boşsa kullanıcının tüm bildirimlerini.</summary>
public sealed record MarkNotificationsReadCommand(Guid? Id) : ICommand<Unit>;

internal sealed class MarkNotificationsReadCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<MarkNotificationsReadCommand, Unit>
{
    public async Task<Result<Unit>> Handle(MarkNotificationsReadCommand command, CancellationToken ct)
    {
        var unread = db.Notifications.Where(n => n.RecipientUserId == currentUser.UserId && n.ReadAt == null);
        if (command.Id is { } id)
        {
            // Başkasının bildirimi "bulunamadı" sayılır.
            if (!await db.Notifications.AnyAsync(n => n.Id == id && n.RecipientUserId == currentUser.UserId, ct))
                return Error.NotFound("Notification.NotFound", "Bildirim bulunamadı.");
            unread = unread.Where(n => n.Id == id);
        }

        var now = clock.GetUtcNow();
        foreach (var notification in await unread.ToListAsync(ct))
            notification.MarkRead(now);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
