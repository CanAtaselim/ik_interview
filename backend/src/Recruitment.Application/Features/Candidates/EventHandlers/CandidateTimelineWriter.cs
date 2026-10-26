using Recruitment.Application.Abstractions;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Candidates.Events;

namespace Recruitment.Application.Features.Candidates.EventHandlers;

/// <summary>Aday üzerindeki olayları zaman çizelgesine yazar. Zaman çizelgesi başka hiçbir yerden yazılmaz.</summary>
internal sealed class CandidateTimelineWriter(IAppDbContext db) :
    IDomainEventHandler<CandidateCreated>,
    IDomainEventHandler<CandidateDocumentAdded>,
    IDomainEventHandler<ScreeningStarted>,
    IDomainEventHandler<ScreeningDeleted>,
    IDomainEventHandler<CandidateTalentPoolChanged>
{
    public Task Handle(CandidateCreated e, CancellationToken ct) => Write(
        e.CandidateId,
        TimelineEntryType.CandidateAdded,
        e.DespiteDuplicate ? "Aday, mükerrer kayıt uyarısına rağmen eklendi." : "Aday eklendi.",
        e.ActorUserId,
        e.OccurredAt,
        ct);

    public Task Handle(CandidateDocumentAdded e, CancellationToken ct) => Write(
        e.CandidateId, TimelineEntryType.DocumentAdded, $"Dosya yüklendi: {e.FileName}", e.ActorUserId, e.OccurredAt, ct);

    public Task Handle(ScreeningStarted e, CancellationToken ct) => Write(
        e.CandidateId, TimelineEntryType.ScreeningStarted, "Ön görüşme kaydı açıldı.", e.ActorUserId, e.OccurredAt, ct);

    public Task Handle(ScreeningDeleted e, CancellationToken ct) => Write(
        e.CandidateId, TimelineEntryType.ScreeningDeleted, "Ön görüşme kaydı silindi.", e.ActorUserId, e.OccurredAt, ct);

    public Task Handle(CandidateTalentPoolChanged e, CancellationToken ct) => Write(
        e.CandidateId,
        e.InTalentPool ? TimelineEntryType.AddedToTalentPool : TimelineEntryType.RemovedFromTalentPool,
        e.InTalentPool ? "Yetenek havuzuna alındı." : "Yetenek havuzundan çıkarıldı.",
        e.ActorUserId,
        e.OccurredAt,
        ct);

    private async Task Write(
        Guid candidateId, TimelineEntryType type, string summary, Guid actorUserId, DateTimeOffset occurredAt, CancellationToken ct)
    {
        db.TimelineEntries.Add(TimelineEntry.Create(candidateId, type, summary, actorUserId, occurredAt));
        await db.SaveChangesAsync(ct);
    }
}
