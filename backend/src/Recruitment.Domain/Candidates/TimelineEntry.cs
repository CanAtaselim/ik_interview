using Recruitment.Domain.Common;

namespace Recruitment.Domain.Candidates;

/// <summary>
/// Aday üzerindeki bir olay: kim, ne zaman, ne yaptı. Domain event handler'ları yazar; elle yazılmaz.
/// </summary>
public sealed class TimelineEntry : Entity
{
    private TimelineEntry() { }

    public Guid CandidateId { get; private set; }
    public TimelineEntryType Type { get; private set; }
    public string Summary { get; private set; } = default!;
    public Guid? ActorUserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static TimelineEntry Create(
        Guid candidateId, TimelineEntryType type, string summary, Guid? actorUserId, DateTimeOffset occurredAt) =>
        new()
        {
            CandidateId = candidateId,
            Type = type,
            Summary = summary,
            ActorUserId = actorUserId,
            OccurredAt = occurredAt,
        };
}
