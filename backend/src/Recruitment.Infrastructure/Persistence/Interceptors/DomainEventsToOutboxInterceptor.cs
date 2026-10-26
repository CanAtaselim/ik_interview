using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Recruitment.Domain.Common;
using Recruitment.Infrastructure.Outbox;

namespace Recruitment.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Entity'lerin ürettiği domain event'leri, onları doğuran değişiklikle aynı transaction'da
/// outbox tablosuna yazar; böylece değişiklik kaydedilip event kaybolamaz.
/// </summary>
internal sealed class DomainEventsToOutboxInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
            return;

        var now = clock.GetUtcNow();
        var entities = context.ChangeTracker.Entries<Entity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        foreach (var entity in entities)
        {
            context.Set<OutboxMessage>().AddRange(entity.DomainEvents.Select(e => OutboxMessage.From(e, now)));
            entity.ClearDomainEvents();
        }
    }
}
