using Recruitment.Domain.Common;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Domain.Interviews;

/// <summary>Mülakatın zamanı, yeri ve katılımcıları; planlama ve yeniden planlamada aynı kurallar uygulanır.</summary>
public sealed record InterviewPlan(
    Guid TypeId,
    InterviewMode Mode,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    Guid? RoomId,
    string? MeetingUrl,
    string? Note,
    IReadOnlyCollection<Guid> InterviewerUserIds,
    Guid? TemplateId = null,
    bool NotifyCandidate = false);

/// <summary>Mülakata atanmış kişi.</summary>
public sealed class InterviewInterviewer
{
    private InterviewInterviewer() { }

    internal InterviewInterviewer(Guid userId) => UserId = userId;

    public Guid UserId { get; private set; }
}

/// <summary>Adayın bir pozisyondaki süreci içinde yapılan mülakat.</summary>
public sealed class Interview : AggregateRoot
{
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 480;
    public const int MaxInterviewers = 10;
    public const int TextMaxLength = 2000;
    public const int MeetingUrlMaxLength = 1000;

    private static readonly TimeSpan DayReminder = TimeSpan.FromHours(24);
    private static readonly TimeSpan HourReminder = TimeSpan.FromHours(1);

    private readonly List<InterviewInterviewer> _interviewers = [];

    private Interview() { }

    public Guid ApplicationId { get; private set; }

    // Takvim ve aday kartı sorguları süreci birleştirmeden çalışsın diye tekrar tutulur.
    public Guid CandidateId { get; private set; }
    public Guid PositionId { get; private set; }

    public Guid TypeId { get; private set; }
    public InterviewMode Mode { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public int DurationMinutes { get; private set; }

    /// <summary>Çakışma sorguları için saklanır (başlangıç + süre).</summary>
    public DateTimeOffset EndsAt { get; private set; }

    public Guid? RoomId { get; private set; }

    /// <summary>Mülakatta sorulacak soruların şablonu (planlamada pozisyonun şablonlarından seçilir).</summary>
    public Guid? TemplateId { get; private set; }

    /// <summary>Planlama, değişiklik ve iptal adaya e-postayla bildirilsin mi?</summary>
    public bool NotifyCandidate { get; private set; }

    /// <summary>Takvim davetinin sürümü (.ics SEQUENCE); her değişiklik ve iptalde artar.</summary>
    public int CalendarSequence { get; private set; }
    public string? MeetingUrl { get; private set; }

    /// <summary>Mülakatçılara not (ör. odaklanılacak konular).</summary>
    public string? Note { get; private set; }

    public InterviewStatus Status { get; private set; }
    public Guid ScheduledByUserId { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset? OutcomeRecordedAt { get; private set; }
    public DateTimeOffset? DayReminderSentAt { get; private set; }
    public DateTimeOffset? HourReminderSentAt { get; private set; }

    /// <summary>Tüm değerlendirmeler gönderildikten sonra İK'nın ortak kararı.</summary>
    public DebriefDecision? DebriefDecision { get; private set; }

    public string? DebriefReason { get; private set; }
    public Guid? DebriefByUserId { get; private set; }
    public DateTimeOffset? DebriefAt { get; private set; }

    public IReadOnlyCollection<InterviewInterviewer> Interviewers => _interviewers;

    public IReadOnlyList<Guid> InterviewerUserIds => [.. _interviewers.Select(i => i.UserId)];

    public static Interview Schedule(
        Guid applicationId, Guid candidateId, Guid positionId, InterviewPlan plan, Guid scheduledByUserId, TimeProvider clock)
    {
        var interview = new Interview
        {
            ApplicationId = applicationId,
            CandidateId = candidateId,
            PositionId = positionId,
            Status = InterviewStatus.Scheduled,
            ScheduledByUserId = scheduledByUserId,
        };
        interview.Apply(plan, clock);
        interview.Raise(new InterviewScheduled(
            interview.Id, candidateId, interview.StartsAt, interview.InterviewerUserIds, scheduledByUserId, clock.GetUtcNow()));
        return interview;
    }

    public void Reschedule(InterviewPlan plan, Guid actorUserId, TimeProvider clock)
    {
        EnsureScheduled();
        var previousStartsAt = StartsAt;
        var previousInterviewers = InterviewerUserIds;
        CalendarSequence++;

        Apply(plan, clock);
        Raise(new InterviewRescheduled(
            Id, CandidateId, previousStartsAt, StartsAt, previousInterviewers, InterviewerUserIds, actorUserId, clock.GetUtcNow()));
    }

    public void Cancel(string reason, Guid actorUserId, TimeProvider clock)
    {
        EnsureScheduled();
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("İptal nedenini yazmalısınız.");

        Status = InterviewStatus.Cancelled;
        CalendarSequence++;
        CancellationReason = reason.Trim();
        Raise(new InterviewCancelled(Id, CandidateId, InterviewerUserIds, actorUserId, clock.GetUtcNow()));
    }

    /// <summary>Mülakatın sonucu: yapıldı veya aday gelmedi. Yalnızca başlangıç saatinden sonra girilir.</summary>
    public void RecordOutcome(InterviewStatus outcome, Guid actorUserId, TimeProvider clock)
    {
        EnsureScheduled();
        if (outcome is not (InterviewStatus.Completed or InterviewStatus.NoShow))
            throw new DomainException("Sonuç olarak yalnızca “yapıldı” veya “aday gelmedi” seçilebilir.");

        var now = clock.GetUtcNow();
        if (now < StartsAt)
            throw new DomainException("Mülakatın sonucu, başlangıç saatinden sonra girilebilir.");

        Status = outcome;
        OutcomeRecordedAt = now;
        Raise(new InterviewOutcomeRecorded(Id, CandidateId, outcome, actorUserId, now));
    }

    /// <summary>
    /// İK'nın ortak kararı. Yalnızca yapılmış mülakatta ve tüm mülakatçılar değerlendirmesini
    /// gönderdikten sonra verilir; yeni karar öncekinin yerine geçer.
    /// </summary>
    public void RecordDebrief(DebriefDecision decision, string reason, bool allScorecardsSubmitted, Guid actorUserId, TimeProvider clock)
    {
        if (Status != InterviewStatus.Completed)
            throw new DomainException("Ortak karar yalnızca yapılmış mülakat için verilir.");
        if (!allScorecardsSubmitted)
            throw new DomainException("Ortak karar için tüm mülakatçıların değerlendirmesini göndermesi gerekir.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Ortak kararın gerekçesini yazmalısınız.");

        var now = clock.GetUtcNow();
        DebriefDecision = decision;
        DebriefReason = reason.Trim();
        DebriefByUserId = actorUserId;
        DebriefAt = now;
        Raise(new InterviewDebriefRecorded(Id, CandidateId, decision, actorUserId, now));
    }

    public bool IsAssignedTo(Guid userId) => _interviewers.Any(i => i.UserId == userId);

    /// <summary>24 saat ve 1 saat kala birer hatırlatma; ikisi birden düşerse yalnızca yakın olan gider.</summary>
    public bool SendDueReminder(TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        var remaining = StartsAt - now;
        if (Status != InterviewStatus.Scheduled || remaining <= TimeSpan.Zero)
            return false;

        if (remaining <= HourReminder && HourReminderSentAt is null)
        {
            HourReminderSentAt = now;
            DayReminderSentAt ??= now;
            Raise(new InterviewReminderDue(Id, InterviewerUserIds, 1, StartsAt));
            return true;
        }

        if (remaining <= DayReminder && DayReminderSentAt is null)
        {
            DayReminderSentAt = now;
            Raise(new InterviewReminderDue(Id, InterviewerUserIds, 24, StartsAt));
            return true;
        }

        return false;
    }

    private void Apply(InterviewPlan plan, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        if (plan.TypeId == Guid.Empty)
            throw new DomainException("Mülakat tipini seçmelisiniz.");
        if (plan.StartsAt <= now)
            throw new DomainException("Geçmiş bir saate mülakat planlanamaz.");
        if (plan.DurationMinutes is < MinDurationMinutes or > MaxDurationMinutes)
            throw new DomainException($"Mülakat süresi {MinDurationMinutes} ile {MaxDurationMinutes} dakika arasında olmalı.");

        var interviewers = plan.InterviewerUserIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (interviewers.Count == 0)
            throw new DomainException("En az bir mülakatçı seçmelisiniz.");
        if (interviewers.Count > MaxInterviewers)
            throw new DomainException($"Bir mülakata en fazla {MaxInterviewers} mülakatçı atanabilir.");

        var timeChanged = plan.StartsAt != StartsAt;

        TypeId = plan.TypeId;
        Mode = plan.Mode;
        StartsAt = plan.StartsAt;
        DurationMinutes = plan.DurationMinutes;
        EndsAt = plan.StartsAt.AddMinutes(plan.DurationMinutes);
        // Yüz yüze mülakatın bağlantısı, online mülakatın odası olmaz.
        RoomId = plan.Mode == InterviewMode.InPerson ? plan.RoomId : null;
        MeetingUrl = plan.Mode == InterviewMode.Online && !string.IsNullOrWhiteSpace(plan.MeetingUrl)
            ? plan.MeetingUrl.Trim()
            : null;
        Note = string.IsNullOrWhiteSpace(plan.Note) ? null : plan.Note.Trim();
        TemplateId = plan.TemplateId;
        NotifyCandidate = plan.NotifyCandidate;

        _interviewers.RemoveAll(i => !interviewers.Contains(i.UserId));
        var added = interviewers.Where(id => !IsAssignedTo(id)).ToList();
        _interviewers.AddRange(added.Select(id => new InterviewInterviewer(id)));

        // Zaman değişince hatırlatmalar yeniden hesaplanır; davet zaten yakın bir saate gönderiliyorsa
        // aynı aralıktaki hatırlatma ayrıca gitmez.
        if (timeChanged)
        {
            var remaining = plan.StartsAt - now;
            DayReminderSentAt = remaining <= DayReminder ? now : null;
            HourReminderSentAt = remaining <= HourReminder ? now : null;
        }
    }

    private void EnsureScheduled()
    {
        if (Status != InterviewStatus.Scheduled)
            throw new DomainException("Yalnızca planlanmış (sonuçlanmamış) mülakat değiştirilebilir.");
    }
}
