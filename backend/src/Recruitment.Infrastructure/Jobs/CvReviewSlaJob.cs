using Hangfire;
using Microsoft.Extensions.Logging;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.CvReviews.ProcessCvReviewDeadlines;

namespace Recruitment.Infrastructure.Jobs;

public sealed class CvReviewSlaJob(IDispatcher dispatcher, ILogger<CvReviewSlaJob> logger)
{
    public const string JobId = "cv-review-sla";

    /// <summary>Her saat başı.</summary>
    public const string Schedule = "0 * * * *";

    [AutomaticRetry(Attempts = 2)]
    public async Task Run(CancellationToken ct)
    {
        var result = await dispatcher.Send(new ProcessCvReviewDeadlinesCommand(), ct);
        if (result.IsSuccess && (result.Value.Reminded > 0 || result.Value.Escalated > 0))
        {
            logger.LogInformation(
                "Geciken CV değerlendirmeleri: {Reminded} hatırlatma, {Escalated} gecikme bildirimi",
                result.Value.Reminded,
                result.Value.Escalated);
        }
    }
}
