using Hangfire;
using Recruitment.Infrastructure.Outbox;

namespace Recruitment.Infrastructure.Jobs;

public sealed class OutboxProcessorJob(OutboxProcessor processor)
{
    public const string JobId = "outbox-processor";

    /// <summary>Her 15 saniyede bir (saniye alanlı cron).</summary>
    public const string Schedule = "*/15 * * * * *";

    // Önceki çalıştırma bitmeden yenisi başlamasın.
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    [AutomaticRetry(Attempts = 0)]
    public Task Run(CancellationToken ct) => processor.ProcessPending(ct);
}
