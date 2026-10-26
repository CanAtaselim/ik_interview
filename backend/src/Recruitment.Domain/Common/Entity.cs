namespace Recruitment.Domain.Common;

/// <summary>
/// Tüm entity'lerin tabanı. Audit ve soft delete alanlarını persistence katmanındaki
/// interceptor doldurur; domain kodu bu alanlara yazmaz.
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _events = [];

    public Guid Id { get; protected set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = default!;
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    protected void Raise(IDomainEvent e) => _events.Add(e);

    public void ClearDomainEvents() => _events.Clear();
}
