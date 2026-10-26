namespace Recruitment.Domain.Audit;

public enum AuditAction
{
    Created,
    Updated,
    Deleted,
}

/// <summary>Bir entity üzerindeki değişikliğin değiştirilemez kaydı. Persistence interceptor'ı yazar.</summary>
public sealed class AuditLog
{
    private AuditLog() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string Username { get; private set; } = default!;
    public DateTimeOffset OccurredAt { get; private set; }
    public string EntityType { get; private set; } = default!;
    public Guid EntityId { get; private set; }
    public AuditAction Action { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }

    public static AuditLog Create(
        string username,
        DateTimeOffset occurredAt,
        string entityType,
        Guid entityId,
        AuditAction action,
        string? oldValues,
        string? newValues) =>
        new()
        {
            Username = username,
            OccurredAt = occurredAt,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValues = oldValues,
            NewValues = newValues,
        };
}
