using Hangfire;
using Microsoft.Extensions.Logging;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Blacklist.ExpireBlacklistEntries;

namespace Recruitment.Infrastructure.Jobs;

public sealed class BlacklistExpiryJob(IDispatcher dispatcher, ILogger<BlacklistExpiryJob> logger)
{
    public const string JobId = "blacklist-expiry";

    /// <summary>Her gün 02:00 (Türkiye saati).</summary>
    public const string Schedule = "0 2 * * *";

    [AutomaticRetry(Attempts = 2)]
    public async Task Run(CancellationToken ct)
    {
        var result = await dispatcher.Send(new ExpireBlacklistEntriesCommand(), ct);
        if (result.IsSuccess && result.Value > 0)
            logger.LogInformation("Süresi dolan {Count} kara liste kaydı sonlandırıldı", result.Value);
    }
}
