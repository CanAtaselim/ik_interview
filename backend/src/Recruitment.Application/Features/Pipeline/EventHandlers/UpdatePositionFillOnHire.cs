using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Pipeline.Events;

namespace Recruitment.Application.Features.Pipeline.EventHandlers;

/// <summary>
/// İşe alımda pozisyonun doluluğunu artırır, geri alınırsa azaltır. Teklif modülü (Faz 2)
/// geldiğinde doluluk teklif kabulüyle artacak; o zamana kadar "İşe alındı" aşaması esas alınır.
/// </summary>
internal sealed class UpdatePositionFillOnHire(IAppDbContext db) :
    IDomainEventHandler<ApplicationHired>,
    IDomainEventHandler<ApplicationHireReverted>
{
    public async Task Handle(ApplicationHired domainEvent, CancellationToken ct)
    {
        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == domainEvent.PositionId, ct);
        if (position is null)
            return;

        position.RecordHire();
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ApplicationHireReverted domainEvent, CancellationToken ct)
    {
        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == domainEvent.PositionId, ct);
        if (position is null)
            return;

        position.RevertHire();
        await db.SaveChangesAsync(ct);
    }
}
