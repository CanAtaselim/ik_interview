using Recruitment.Domain.Common;

namespace Recruitment.Application.Abstractions;

/// <summary>
/// Bir domain event'e tepki veren iş (pozisyon açma, bildirim, e-posta…). Outbox'tan çağrılır ve
/// aynı event iki kez gelebileceği için idempotent olmalıdır.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken ct);
}

public interface IDomainEventDispatcher
{
    /// <summary>Event'in tipine kayıtlı tüm handler'ları sırayla çalıştırır.</summary>
    Task Dispatch(IDomainEvent domainEvent, CancellationToken ct);
}

/// <summary>
/// Olay işleme transaction'ı commit edildikten sonra yapılacak işler (ör. anlık bildirim). Commit
/// öncesi iletilen bildirimi istemci okumaya kalkarsa henüz kaydedilmemiş veriyi görürdü; işlem
/// geri alınırsa kuyruk atılır.
/// </summary>
public interface IAfterCommitQueue
{
    void Enqueue(Func<CancellationToken, Task> action);

    Task RunAll(CancellationToken ct);
}
