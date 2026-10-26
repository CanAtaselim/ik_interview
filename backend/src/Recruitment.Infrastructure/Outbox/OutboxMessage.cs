using System.Text.Json;
using Recruitment.Domain.Common;

namespace Recruitment.Infrastructure.Outbox;

/// <summary>Bir domain event'in, onu üreten değişiklikle aynı transaction'da saklanan kopyası.</summary>
public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int Attempts { get; private set; }

    public static OutboxMessage From(IDomainEvent domainEvent, DateTimeOffset occurredAt)
    {
        var type = domainEvent.GetType();

        return new OutboxMessage
        {
            Type = type.FullName!,
            Payload = JsonSerializer.Serialize(domainEvent, type),
            OccurredAt = occurredAt,
        };
    }

    /// <summary>Mesajı üreten domain event'i geri kurar; tip artık yoksa hata fırlatır.</summary>
    public IDomainEvent Deserialize()
    {
        var type = typeof(IDomainEvent).Assembly.GetType(Type)
            ?? throw new InvalidOperationException($"Outbox mesajının tipi bulunamadı: {Type}");

        return (IDomainEvent)JsonSerializer.Deserialize(Payload, type)!;
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        Error = null;
    }
}
