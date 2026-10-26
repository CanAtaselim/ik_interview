using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Interviews;

namespace Recruitment.Application.Features.Interviews.SendInterviewReminders;

/// <summary>
/// 24 saat ve 1 saat içinde başlayacak mülakatlar için hatırlatma event'i üretir. 15 dakikada bir
/// zamanlanmış işten çağrılır; her hatırlatma bir kez gider.
/// </summary>
public sealed record SendInterviewRemindersCommand : ICommand<int>;

internal sealed class SendInterviewRemindersCommandHandler(IAppDbContext db, TimeProvider clock)
    : ICommandHandler<SendInterviewRemindersCommand, int>
{
    public async Task<Result<int>> Handle(SendInterviewRemindersCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var dayAhead = now.AddHours(24);
        var hourAhead = now.AddHours(1);

        var due = await db.Interviews
            .Where(i => i.Status == InterviewStatus.Scheduled && i.StartsAt > now &&
                        ((i.DayReminderSentAt == null && i.StartsAt <= dayAhead) ||
                         (i.HourReminderSentAt == null && i.StartsAt <= hourAhead)))
            .ToListAsync(ct);

        var sent = due.Count(interview => interview.SendDueReminder(clock));
        await db.SaveChangesAsync(ct);
        return sent;
    }
}
