using Hangfire;
using Microsoft.Extensions.Logging;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Notifications.DispatchOutgoingEmails;

namespace Recruitment.Infrastructure.Jobs;

public sealed class EmailDispatchJob(IDispatcher dispatcher, ILogger<EmailDispatchJob> logger)
{
    public const string JobId = "email-dispatch";

    /// <summary>Dakikada bir.</summary>
    public const string Schedule = "* * * * *";

    // Önceki çalıştırma bitmeden yenisi başlamasın; aynı e-posta iki kez gönderilmesin.
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    [AutomaticRetry(Attempts = 0)]
    public async Task Run(CancellationToken ct)
    {
        var result = await dispatcher.Send(new DispatchOutgoingEmailsCommand(), ct);
        if (result.IsSuccess && result.Value > 0)
            logger.LogInformation("{Count} e-posta gönderildi", result.Value);
    }
}
