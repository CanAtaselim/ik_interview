using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace Recruitment.Infrastructure.Jobs;

public static class RecurringJobs
{
    /// <summary>Zamanlanmış işleri Hangfire'a kaydeder; her açılışta çağrılabilir (idempotent).</summary>
    public static void RegisterRecurringJobs(this IServiceProvider services)
    {
        var jobs = services.GetRequiredService<IRecurringJobManager>();

        jobs.AddOrUpdate<OutboxProcessorJob>(
            OutboxProcessorJob.JobId, job => job.Run(CancellationToken.None), OutboxProcessorJob.Schedule);

        jobs.AddOrUpdate<BlacklistExpiryJob>(
            BlacklistExpiryJob.JobId,
            job => job.Run(CancellationToken.None),
            BlacklistExpiryJob.Schedule,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul") });

        jobs.AddOrUpdate<CvReviewSlaJob>(
            CvReviewSlaJob.JobId, job => job.Run(CancellationToken.None), CvReviewSlaJob.Schedule);

        jobs.AddOrUpdate<InterviewReminderJob>(
            InterviewReminderJob.JobId, job => job.Run(CancellationToken.None), InterviewReminderJob.Schedule);

        jobs.AddOrUpdate<EmailDispatchJob>(
            EmailDispatchJob.JobId, job => job.Run(CancellationToken.None), EmailDispatchJob.Schedule);
    }
}
