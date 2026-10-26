using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.HiringRequests.Events;
using Recruitment.Domain.Positions;

namespace Recruitment.Application.Features.Positions;

/// <summary>Son onayı alan kadro talebi için pozisyonu otomatik açar; sorumlu İK'yı sonradan İK atar.</summary>
internal sealed class OpenPositionWhenHiringRequestApproved(IAppDbContext db)
    : IDomainEventHandler<HiringRequestApproved>
{
    public async Task Handle(HiringRequestApproved domainEvent, CancellationToken ct)
    {
        // Aynı event iki kez gelebilir; talep için pozisyon zaten açıldıysa (silinmiş olsa bile) tekrar açılmaz.
        var alreadyOpened = await db.Positions.IgnoreQueryFilters()
            .AnyAsync(p => p.HiringRequestId == domainEvent.HiringRequestId, ct);
        if (alreadyOpened)
            return;

        var request = await db.HiringRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == domainEvent.HiringRequestId, ct);
        if (request is null)
            return;

        db.Positions.Add(Position.Open(
            request.Title,
            request.DepartmentId,
            request.LevelId,
            request.Headcount,
            request.Description,
            responsibleHrUserId: null,
            hiringRequestId: request.Id));

        await db.SaveChangesAsync(ct);
    }
}
