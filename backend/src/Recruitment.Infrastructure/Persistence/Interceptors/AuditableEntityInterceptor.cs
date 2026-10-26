using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Common;

namespace Recruitment.Infrastructure.Persistence.Interceptors;

/// <summary>Created/Updated alanlarını doldurur ve silmeyi soft delete'e çevirir.</summary>
internal sealed class AuditableEntityInterceptor(ICurrentUser currentUser, TimeProvider clock) : SaveChangesInterceptor
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
        var actor = currentUser.ActorName();

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(e => e.CreatedAt).CurrentValue = now;
                    entry.Property(e => e.CreatedBy).CurrentValue = actor;
                    break;

                case EntityState.Modified:
                    entry.Property(e => e.UpdatedAt).CurrentValue = now;
                    entry.Property(e => e.UpdatedBy).CurrentValue = actor;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Unchanged;
                    entry.Property(e => e.IsDeleted).CurrentValue = true;
                    entry.Property(e => e.DeletedAt).CurrentValue = now;
                    entry.Property(e => e.DeletedBy).CurrentValue = actor;
                    KeepOwnedDataUnchanged(entry);
                    break;
            }
        }
    }

    /// <summary>
    /// Silinen kaydın sahip olduğu alt kayıtlar (ör. JSON kolonunda tutulan eğitim ve dil satırları) da
    /// "silinmiş" işaretlenir; satır soft delete olduğu için bunlar değişmeden kalmalı, yoksa EF JSON'u yazarken hata verir.
    /// </summary>
    private static void KeepOwnedDataUnchanged(EntityEntry owner)
    {
        foreach (var collection in owner.Collections)
        {
            if (collection.Metadata is not INavigationBase { TargetEntityType: var target } || !target.IsOwned()
                || collection.CurrentValue is not { } items)
                continue;

            foreach (var item in items)
                owner.Context.Entry(item).State = EntityState.Unchanged;
        }

        foreach (var reference in owner.References)
        {
            if (reference.Metadata is INavigationBase { TargetEntityType: var target } && target.IsOwned()
                && reference.TargetEntry is { } child)
                child.State = EntityState.Unchanged;
        }
    }
}
