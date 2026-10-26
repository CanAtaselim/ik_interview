using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Common;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Her entity değişikliğini eski/yeni değerleriyle <see cref="AuditLog"/> olarak aynı kayıt
/// işlemine ekler. <see cref="AuditableEntityInterceptor"/>'dan sonra çalışmalıdır.
/// </summary>
internal sealed class AuditLogInterceptor(ICurrentUser currentUser, TimeProvider clock) : SaveChangesInterceptor
{
    // Oturum kayıtları iş verisi değildir ve token hash'i içerir.
    private static readonly HashSet<Type> ExcludedTypes = [typeof(RefreshToken)];

    // Audit kaydının kendi kolonlarında zaten bulunan alanlar değer listesinde tekrarlanmaz.
    private static readonly HashSet<string> BookkeepingProperties =
    [
        nameof(Entity.CreatedAt), nameof(Entity.CreatedBy),
        nameof(Entity.UpdatedAt), nameof(Entity.UpdatedBy),
        nameof(Entity.DeletedAt), nameof(Entity.DeletedBy),
    ];

    // Başka alanlardan türetilen, yalnızca arama için tutulan büyük metinler audit kaydına yazılmaz.
    private static readonly HashSet<string> DerivedProperties =
        [nameof(Candidate.SearchText), nameof(CandidateDocument.ExtractedText)];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

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

        var logs = context.ChangeTracker.Entries<Entity>()
            .Where(entry => !ExcludedTypes.Contains(entry.Entity.GetType()))
            .Select(entry => entry.State switch
            {
                EntityState.Added => ForCreated(entry, actor, now),
                EntityState.Modified => ForModified(entry, actor, now),
                _ => null,
            })
            .OfType<AuditLog>()
            .ToList();

        context.Set<AuditLog>().AddRange(logs);
    }

    private static AuditLog ForCreated(EntityEntry<Entity> entry, string actor, DateTimeOffset now)
    {
        var values = entry.Properties
            .Where(p => IsTracked(p) && p.Metadata.Name != nameof(Entity.IsDeleted) && p.CurrentValue is not null)
            .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);

        return AuditLog.Create(
            actor, now, entry.Metadata.ClrType.Name, entry.Entity.Id, AuditAction.Created, null, Serialize(values));
    }

    private static AuditLog? ForModified(EntityEntry<Entity> entry, string actor, DateTimeOffset now)
    {
        var changed = entry.Properties.Where(p => IsTracked(p) && p.IsModified).ToList();
        if (changed.Count == 0)
            return null;

        var softDeleted = changed.Any(p => p.Metadata.Name == nameof(Entity.IsDeleted) && p.CurrentValue is true);

        return AuditLog.Create(
            actor,
            now,
            entry.Metadata.ClrType.Name,
            entry.Entity.Id,
            softDeleted ? AuditAction.Deleted : AuditAction.Updated,
            Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue)),
            Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)));
    }

    private static bool IsTracked(PropertyEntry property) =>
        !property.Metadata.IsPrimaryKey() &&
        !BookkeepingProperties.Contains(property.Metadata.Name) &&
        !DerivedProperties.Contains(property.Metadata.Name) &&
        // Veritabanının hesapladığı kolonlar (ör. arama vektörü) uygulama verisi değildir.
        property.Metadata.GetComputedColumnSql() is null;

    private static string Serialize(Dictionary<string, object?> values) => JsonSerializer.Serialize(values, JsonOptions);
}
