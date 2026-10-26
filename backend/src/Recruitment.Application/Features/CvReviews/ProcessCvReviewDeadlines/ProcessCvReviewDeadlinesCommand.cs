using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.CvReviews;

namespace Recruitment.Application.Features.CvReviews.ProcessCvReviewDeadlines;

public sealed record CvReviewDeadlineResult(int Reminded, int Escalated);

/// <summary>
/// Süresi dolan değerlendirmeler için takım liderine hatırlatma, hâlâ karar yoksa İK'ya gecikme
/// event'i üretir. Saatlik zamanlanmış işten çağrılır; her değerlendirme için birer kez çalışır.
/// </summary>
public sealed record ProcessCvReviewDeadlinesCommand : ICommand<CvReviewDeadlineResult>;

internal sealed class ProcessCvReviewDeadlinesCommandHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<ProcessCvReviewDeadlinesCommand, CvReviewDeadlineResult>
{
    public async Task<Result<CvReviewDeadlineResult>> Handle(ProcessCvReviewDeadlinesCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = await db.CvReviews
            .Where(r => r.Decision == CvReviewDecision.Pending &&
                        ((r.ReminderSentAt == null && r.DueAt < now) || (r.EscalatedAt == null && r.EscalateAt < now)))
            .ToListAsync(ct);

        var reminded = due.Count(review => review.RemindIfOverdue(clock));
        var escalated = due.Count(review => review.EscalateIfDue(clock));
        await db.SaveChangesAsync(ct);
        return new CvReviewDeadlineResult(reminded, escalated);
    }
}
