using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Application.Features.Scorecards;
using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Notifications;
using Recruitment.Domain.Scorecards;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Notifications.EventHandlers;

/// <summary>Değerlendirmeler tamamlanınca ortak karar çağrısı, düzeltmeye açılma ve kara liste onayı.</summary>
internal sealed class ScorecardAndBlacklistNotifications(IAppDbContext db, Notifier notifier) :
    IDomainEventHandler<ScorecardSubmitted>,
    IDomainEventHandler<ScorecardReopened>,
    IDomainEventHandler<BlacklistEntryRequested>
{
    public async Task Handle(ScorecardSubmitted e, CancellationToken ct)
    {
        var interview = await db.Interviews.AsNoTracking().FirstAsync(i => i.Id == e.InterviewId, ct);
        var submitted = await db.SubmittedPairs([interview.Id], ct);
        if (!interview.InterviewerUserIds.All(userId => submitted.Contains((interview.Id, userId))))
            return;

        var (candidate, type) = await Describe(interview.CandidateId, interview.TypeId, ct);
        await notifier.Notify(
            [interview.ScheduledByUserId],
            NotificationType.DebriefReady,
            $"Tüm değerlendirmeler geldi: {candidate} · {type}",
            "Puanları inceleyip ortak kararı kaydedebilirsiniz.",
            $"/interviews/{interview.Id}",
            $"debrief-ready:{interview.Id}:{e.OccurredAt.UtcTicks}",
            ct);
    }

    public async Task Handle(ScorecardReopened e, CancellationToken ct)
    {
        var interview = await db.Interviews.AsNoTracking().FirstAsync(i => i.Id == e.InterviewId, ct);
        var (candidate, type) = await Describe(interview.CandidateId, interview.TypeId, ct);
        await notifier.Notify(
            [e.InterviewerUserId],
            NotificationType.ScorecardReopened,
            $"Değerlendirmeniz düzeltme için açıldı: {candidate} · {type}",
            $"Gerekçe: {e.Reason}",
            $"/interviews/{interview.Id}/scorecard",
            $"scorecard-reopened:{e.ScorecardId}:{e.OccurredAt.UtcTicks}",
            ct);
    }

    public async Task Handle(BlacklistEntryRequested e, CancellationToken ct)
    {
        var candidate = await db.Candidates.Where(c => c.Id == e.CandidateId).Select(c => c.FirstName + " " + c.LastName).FirstAsync(ct);
        var requester = await db.Users.Where(u => u.Id == e.RequestedByUserId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
        await notifier.Notify(
            await notifier.UsersInRole(Role.HrManager, null, ct),
            NotificationType.BlacklistApprovalPending,
            $"Onay bekleyen kara liste talebi: {candidate}",
            $"{requester} talep etti.",
            $"/blacklist?selected={e.EntryId}",
            $"blacklist-requested:{e.EntryId}",
            ct);
    }

    private async Task<(string Candidate, string Type)> Describe(Guid candidateId, Guid typeId, CancellationToken ct)
    {
        var candidate = await db.Candidates.Where(c => c.Id == candidateId).Select(c => c.FirstName + " " + c.LastName).FirstAsync(ct);
        var type = (await db.ItemNames([typeId], ct)).GetValueOrDefault(typeId, "Mülakat");
        return (candidate, type);
    }
}
