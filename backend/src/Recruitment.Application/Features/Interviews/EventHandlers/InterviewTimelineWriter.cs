using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Interviews;

namespace Recruitment.Application.Features.Interviews.EventHandlers;

/// <summary>Mülakat planlama, değişiklik, iptal ve sonuçlarını adayın zaman çizelgesine yazar.</summary>
internal sealed class InterviewTimelineWriter(IAppDbContext db) :
    IDomainEventHandler<InterviewScheduled>,
    IDomainEventHandler<InterviewRescheduled>,
    IDomainEventHandler<InterviewCancelled>,
    IDomainEventHandler<InterviewOutcomeRecorded>
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public async Task Handle(InterviewScheduled e, CancellationToken ct) => await Write(
        e.CandidateId, TimelineEntryType.InterviewScheduled, $"Mülakat planlandı: {await Describe(e.InterviewId, ct, e.StartsAt)}",
        e.ActorUserId, e.OccurredAt, ct);

    public async Task Handle(InterviewRescheduled e, CancellationToken ct)
    {
        // Olay sonraki değişikliklerden önce yazılmış olabilir; saat event'ten okunur, veritabanından değil.
        var details = await Describe(e.InterviewId, ct, e.StartsAt);
        var summary = e.StartsAt == e.PreviousStartsAt
            ? $"Mülakat bilgileri güncellendi: {details}"
            : $"Mülakat {Format(e.PreviousStartsAt)} yerine yeni saate alındı: {details}";
        await Write(e.CandidateId, TimelineEntryType.InterviewRescheduled, summary, e.ActorUserId, e.OccurredAt, ct);
    }

    public async Task Handle(InterviewCancelled e, CancellationToken ct) => await Write(
        e.CandidateId, TimelineEntryType.InterviewCancelled, $"Mülakat iptal edildi: {await Describe(e.InterviewId, ct)}",
        e.ActorUserId, e.OccurredAt, ct);

    public async Task Handle(InterviewOutcomeRecorded e, CancellationToken ct)
    {
        var details = await Describe(e.InterviewId, ct);
        var (type, summary) = e.Status == InterviewStatus.NoShow
            ? (TimelineEntryType.InterviewNoShow, $"Aday mülakata gelmedi: {details}")
            : (TimelineEntryType.InterviewCompleted, $"Mülakat yapıldı: {details}");
        await Write(e.CandidateId, type, summary, e.ActorUserId, e.OccurredAt, ct);
    }

    // "Teknik · “Backend Geliştirici” · 20.10.2026 14:00" (İstanbul saatiyle).
    private async Task<string> Describe(Guid interviewId, CancellationToken ct, DateTimeOffset? startsAt = null)
    {
        var interview = await db.Interviews.AsNoTracking()
            .Where(i => i.Id == interviewId)
            .Select(i => new { i.TypeId, i.PositionId, i.StartsAt })
            .FirstAsync(ct);
        var type = (await db.ItemNames([interview.TypeId], ct)).GetValueOrDefault(interview.TypeId, string.Empty);
        var position = await db.Positions.Where(p => p.Id == interview.PositionId).Select(p => p.Title).FirstOrDefaultAsync(ct);

        return $"{type} · “{position}” · {Format(startsAt ?? interview.StartsAt)}";
    }

    private static string Format(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Zone).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    private async Task Write(
        Guid candidateId, TimelineEntryType type, string summary, Guid actorUserId, DateTimeOffset occurredAt, CancellationToken ct)
    {
        db.TimelineEntries.Add(TimelineEntry.Create(candidateId, type, summary, actorUserId, occurredAt));
        await db.SaveChangesAsync(ct);
    }
}
