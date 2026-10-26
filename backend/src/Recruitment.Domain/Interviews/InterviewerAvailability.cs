using Recruitment.Domain.Common;

namespace Recruitment.Domain.Interviews;

/// <summary>
/// Mülakatçının mülakat için ayırdığı zaman aralığı (Faz 1: sistem içi müsaitlik; Faz 2'de Outlook
/// takviminden okunacak). Planlamada yol gösterir; dışında planlama engellenmez.
/// </summary>
public sealed class InterviewerAvailability : Entity
{
    public static readonly TimeSpan MaxLength = TimeSpan.FromHours(14);

    private InterviewerAvailability() { }

    public Guid UserId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }

    public static InterviewerAvailability Create(Guid userId, DateTimeOffset startsAt, DateTimeOffset endsAt, TimeProvider clock)
    {
        if (endsAt <= startsAt)
            throw new DomainException("Bitiş saati başlangıçtan sonra olmalı.");
        if (endsAt - startsAt > MaxLength)
            throw new DomainException("Tek bir müsaitlik aralığı en fazla 14 saat olabilir.");
        if (endsAt <= clock.GetUtcNow())
            throw new DomainException("Geçmiş bir zaman için müsaitlik eklenemez.");

        return new InterviewerAvailability { UserId = userId, StartsAt = startsAt, EndsAt = endsAt };
    }

    public bool Overlaps(DateTimeOffset startsAt, DateTimeOffset endsAt) => StartsAt < endsAt && startsAt < EndsAt;
}
