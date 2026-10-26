using Hangfire;
using Microsoft.Extensions.Logging;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Interviews.SendInterviewReminders;

namespace Recruitment.Infrastructure.Jobs;

public sealed class InterviewReminderJob(IDispatcher dispatcher, ILogger<InterviewReminderJob> logger)
{
    public const string JobId = "interview-reminder";

    /// <summary>15 dakikada bir.</summary>
    public const string Schedule = "*/15 * * * *";

    [AutomaticRetry(Attempts = 2)]
    public async Task Run(CancellationToken ct)
    {
        var result = await dispatcher.Send(new SendInterviewRemindersCommand(), ct);
        if (result.IsSuccess && result.Value > 0)
            logger.LogInformation("{Count} mülakat için hatırlatma üretildi", result.Value);
    }
}
