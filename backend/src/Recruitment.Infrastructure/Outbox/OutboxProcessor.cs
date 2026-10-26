using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recruitment.Application.Abstractions;
using Recruitment.Infrastructure.Persistence;

namespace Recruitment.Infrastructure.Outbox;

/// <summary>
/// Bekleyen outbox mesajlarını sırayla işler. Her mesaj kendi transaction'ında, handler'ların
/// yaptığı değişikliklerle birlikte kaydedilir: ya hepsi olur ya hiçbiri. Hata alan mesaj
/// en fazla <see cref="MaxAttempts"/> kez denenir.
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<OutboxProcessor> logger)
{
    private const int MaxAttempts = 5;
    private const int MaxMessagesPerRun = 100;
    private const int MaxErrorLength = 2000;

    /// <returns>Başarıyla işlenen mesaj sayısı.</returns>
    public async Task<int> ProcessPending(CancellationToken ct = default)
    {
        // Bu çalıştırmada hata alan mesajlar sonraki çalıştırmaya kalır; yoksa tüm denemeler art arda tüketilirdi.
        var failedInThisRun = new List<Guid>();
        var processed = 0;

        while (processed + failedInThisRun.Count < MaxMessagesPerRun)
        {
            var outcome = await ProcessNext(failedInThisRun, ct);
            if (outcome is null)
                break;

            if (outcome.Value.Succeeded)
                processed++;
            else
                failedInThisRun.Add(outcome.Value.MessageId);
        }

        return processed;
    }

    private async Task<(Guid MessageId, bool Succeeded)?> ProcessNext(List<Guid> skip, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // SKIP LOCKED: aynı anda çalışan başka bir işleyici aynı mesajı almaz.
        var skipIds = skip.ToArray();
        var message = (await db.OutboxMessages
            .FromSql($"""
                select * from outbox_messages
                where processed_at is null and attempts < {MaxAttempts} and id <> all({skipIds})
                order by occurred_at, id
                limit 1
                for update skip locked
                """)
            .ToListAsync(ct)).SingleOrDefault();
        if (message is null)
            return null;

        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            await dispatcher.Dispatch(message.Deserialize(), ct);

            message.MarkProcessed(clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Anlık bildirim gibi işler ancak kayıt kesinleştikten sonra yapılır.
            await scope.ServiceProvider.GetRequiredService<IAfterCommitQueue>().RunAll(ct);
            return (message.Id, true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "Outbox mesajı işlenemedi: {MessageId} ({MessageType})", message.Id, message.Type);
            await RecordFailure(message.Id, ex.Message, ct);
            return (message.Id, false);
        }
    }

    // Handler'ın yarım kalan değişiklikleri geri alındığı için hata ayrı bir bağlamda kaydedilir.
    private async Task RecordFailure(Guid messageId, string error, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trimmed = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;

        await db.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(m => m.Attempts, m => m.Attempts + 1)
                    .SetProperty(m => m.Error, trimmed),
                ct);
    }
}
