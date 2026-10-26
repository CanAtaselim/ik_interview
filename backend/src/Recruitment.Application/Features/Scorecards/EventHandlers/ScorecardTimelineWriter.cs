using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Scorecards.EventHandlers;

/// <summary>
/// Değerlendirme gönderme, yeniden açma ve ortak kararı adayın zaman çizelgesine yazar.
/// Puanlar ve notlar çizelgeye yazılmaz; yalnızca öneri ve karar.
/// </summary>
internal sealed class ScorecardTimelineWriter(IAppDbContext db) :
    IDomainEventHandler<ScorecardSubmitted>,
    IDomainEventHandler<ScorecardReopened>,
    IDomainEventHandler<InterviewDebriefRecorded>
{
    public async Task Handle(ScorecardSubmitted e, CancellationToken ct)
    {
        var (candidateId, type) = await Interview(e.InterviewId, ct);
        await Write(candidateId, TimelineEntryType.ScorecardSubmitted,
            $"{await UserName(e.InterviewerUserId, ct)} {type} mülakatı değerlendirmesini gönderdi: {e.Recommendation.Label()}.",
            e.InterviewerUserId, e.OccurredAt, ct);
    }

    public async Task Handle(ScorecardReopened e, CancellationToken ct)
    {
        var (candidateId, type) = await Interview(e.InterviewId, ct);
        await Write(candidateId, TimelineEntryType.ScorecardReopened,
            $"{await UserName(e.InterviewerUserId, ct)} adlı mülakatçının {type} mülakatı değerlendirmesi düzeltme için açıldı: {e.Reason}",
            e.ActorUserId, e.OccurredAt, ct);
    }

    public async Task Handle(InterviewDebriefRecorded e, CancellationToken ct)
    {
        var (_, type) = await Interview(e.InterviewId, ct);
        await Write(e.CandidateId, TimelineEntryType.DebriefRecorded,
            $"{type} mülakatı ortak kararı: {e.Decision.Label()}.", e.ActorUserId, e.OccurredAt, ct);
    }

    private async Task<(Guid CandidateId, string Type)> Interview(Guid interviewId, CancellationToken ct)
    {
        var interview = await db.Interviews.AsNoTracking()
            .Where(i => i.Id == interviewId)
            .Select(i => new { i.CandidateId, i.TypeId })
            .FirstAsync(ct);
        var type = (await db.ItemNames([interview.TypeId], ct)).GetValueOrDefault(interview.TypeId, string.Empty);
        return (interview.CandidateId, type);
    }

    private async Task<string> UserName(Guid userId, CancellationToken ct) =>
        await db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? string.Empty;

    private async Task Write(
        Guid candidateId, TimelineEntryType type, string summary, Guid actorUserId, DateTimeOffset occurredAt, CancellationToken ct)
    {
        db.TimelineEntries.Add(TimelineEntry.Create(candidateId, type, summary, actorUserId, occurredAt));
        await db.SaveChangesAsync(ct);
    }
}
