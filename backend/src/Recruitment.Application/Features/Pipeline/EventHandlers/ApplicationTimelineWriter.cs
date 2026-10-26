using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Pipeline.Events;

namespace Recruitment.Application.Features.Pipeline.EventHandlers;

/// <summary>Süreçteki her değişikliği adayın zaman çizelgesine yazar.</summary>
internal sealed class ApplicationTimelineWriter(IAppDbContext db) :
    IDomainEventHandler<ApplicationStarted>,
    IDomainEventHandler<ApplicationStageChanged>,
    IDomainEventHandler<ApplicationClosed>,
    IDomainEventHandler<ApplicationChangeUndone>
{
    public async Task Handle(ApplicationStarted e, CancellationToken ct) => await Write(
        e.CandidateId,
        TimelineEntryType.ApplicationStarted,
        $"“{await PositionTitle(e.ApplicationId, ct)}” pozisyonu için süreç başlatıldı.",
        e.ActorUserId,
        e.OccurredAt,
        ct);

    public async Task Handle(ApplicationStageChanged e, CancellationToken ct)
    {
        var stageNames = await db.PipelineTemplates.AsNoTracking()
            .SelectMany(t => t.Stages)
            .Where(s => s.Id == e.FromStageId || s.Id == e.ToStageId)
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        await Write(
            e.CandidateId,
            TimelineEntryType.StageChanged,
            $"“{await PositionTitle(e.ApplicationId, ct)}”: {stageNames.GetValueOrDefault(e.FromStageId)} → {stageNames.GetValueOrDefault(e.ToStageId)}" +
            (e.BypassReason is null ? string.Empty : $" (değerlendirmeler beklenmeden geçirildi: {e.BypassReason})"),
            e.ActorUserId,
            e.OccurredAt,
            ct);
    }

    public async Task Handle(ApplicationClosed e, CancellationToken ct)
    {
        var reasonId = await db.JobApplications
            .Where(a => a.Id == e.ApplicationId)
            .Select(a => a.RejectionReasonId)
            .FirstOrDefaultAsync(ct);
        var reason = reasonId is { } id ? (await db.ItemNames([id], ct)).GetValueOrDefault(id) : null;
        var title = await PositionTitle(e.ApplicationId, ct);

        var summary = e.Status == ApplicationStatus.Withdrawn
            ? $"Aday “{title}” sürecinden çekildi"
            : $"“{title}” süreci olumsuz sonuçlandı";

        await Write(
            e.CandidateId,
            TimelineEntryType.ApplicationClosed,
            reason is null ? $"{summary}." : $"{summary}: {reason}",
            e.ActorUserId,
            e.OccurredAt,
            ct);
    }

    public async Task Handle(ApplicationChangeUndone e, CancellationToken ct) => await Write(
        e.CandidateId,
        TimelineEntryType.ChangeUndone,
        $"“{await PositionTitle(e.ApplicationId, ct)}” sürecindeki son değişiklik geri alındı.",
        e.ActorUserId,
        e.OccurredAt,
        ct);

    private async Task<string> PositionTitle(Guid applicationId, CancellationToken ct)
    {
        var positions = db.Positions;

        return await db.JobApplications
            .Where(a => a.Id == applicationId)
            .Select(a => positions.Where(p => p.Id == a.PositionId).Select(p => p.Title).FirstOrDefault())
            .FirstOrDefaultAsync(ct) ?? string.Empty;
    }

    private async Task Write(
        Guid candidateId, TimelineEntryType type, string summary, Guid actorUserId, DateTimeOffset occurredAt, CancellationToken ct)
    {
        db.TimelineEntries.Add(TimelineEntry.Create(candidateId, type, summary, actorUserId, occurredAt));
        await db.SaveChangesAsync(ct);
    }
}
