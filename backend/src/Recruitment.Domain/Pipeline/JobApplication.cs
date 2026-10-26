using Recruitment.Domain.Common;
using Recruitment.Domain.Pipeline.Events;

namespace Recruitment.Domain.Pipeline;

/// <summary>Sürecin geçmişindeki bir değişiklik: aşama geçişi veya sürecin sonlanması.</summary>
public sealed class ApplicationStageChange : Entity
{
    private ApplicationStageChange() { }

    internal ApplicationStageChange(
        Guid fromStageId,
        Guid toStageId,
        ApplicationStatus fromStatus,
        ApplicationStatus toStatus,
        DateTimeOffset previousStageEnteredAt,
        Guid changedByUserId,
        DateTimeOffset changedAt,
        string? bypassReason = null)
    {
        BypassReason = bypassReason;
        FromStageId = fromStageId;
        ToStageId = toStageId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        PreviousStageEnteredAt = previousStageEnteredAt;
        ChangedByUserId = changedByUserId;
        ChangedAt = changedAt;
    }

    public Guid FromStageId { get; private set; }
    public Guid ToStageId { get; private set; }
    public ApplicationStatus FromStatus { get; private set; }
    public ApplicationStatus ToStatus { get; private set; }

    /// <summary>Geri almada "aşamadaki gün sayısı" doğru kalsın diye saklanır.</summary>
    public DateTimeOffset PreviousStageEnteredAt { get; private set; }

    public Guid ChangedByUserId { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
    public DateTimeOffset? UndoneAt { get; private set; }

    /// <summary>İK Yöneticisi bekleyen değerlendirmelere rağmen geçişe izin verdiyse gerekçesi.</summary>
    public string? BypassReason { get; private set; }

    internal void MarkUndone(DateTimeOffset at) => UndoneAt = at;
}

/// <summary>
/// Bir adayın bir pozisyondaki süreci (sözlükteki "Application"; ad, uygulama katmanının ad
/// alanıyla çakışmasın diye JobApplication). Aday aynı anda birden çok pozisyonda süreçte olabilir.
/// </summary>
public sealed class JobApplication : AggregateRoot
{
    public static readonly TimeSpan UndoWindow = TimeSpan.FromMinutes(10);

    private readonly List<ApplicationStageChange> _history = [];

    private JobApplication() { }

    public Guid CandidateId { get; private set; }
    public Guid PositionId { get; private set; }
    public Guid StageId { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset StageEnteredAt { get; private set; }
    public Guid? RejectionReasonId { get; private set; }
    public string? ClosingNote { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyCollection<ApplicationStageChange> History => _history;

    public static JobApplication Start(
        Guid candidateId, Guid positionId, PipelineStageDefinition firstStage, Guid startedByUserId, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        var application = new JobApplication
        {
            CandidateId = candidateId,
            PositionId = positionId,
            StageId = firstStage.Id,
            Status = ApplicationStatus.Active,
            StartedAt = now,
            StageEnteredAt = now,
        };
        application.Raise(new ApplicationStarted(application.Id, candidateId, positionId, startedByUserId, now));
        return application;
    }

    /// <param name="bypassReason">
    /// İK Yöneticisinin bekleyen değerlendirmelere rağmen geçiş gerekçesi (<see cref="StageTransitionContext.BypassedByHrManager"/>).
    /// </param>
    public void MoveTo(
        PipelineTemplate template,
        Guid targetStageId,
        StageTransitionContext context,
        Guid movedByUserId,
        TimeProvider clock,
        string? bypassReason = null)
    {
        if (context.BypassedByHrManager && string.IsNullOrWhiteSpace(bypassReason))
            throw new DomainException("Değerlendirmeler beklenmeden geçiş için gerekçe yazmalısınız.");

        EnsureActive();
        template.CheckAdvance(StageId, targetStageId).ThrowIfDenied();

        var current = template.FindStage(StageId)!;
        var target = template.FindStage(targetStageId)!;
        StageStates.For(current.Kind).CanMoveTo(target, context).ThrowIfDenied();

        var now = clock.GetUtcNow();
        var hired = target.Kind == StageKind.Hired;
        // Gerekçe yalnızca izin olmasa geçiş reddedilecek idiyse (kural gerçekten aşıldıysa) saklanır.
        var usedBypass = context.BypassedByHrManager &&
                         !StageStates.For(current.Kind).CanMoveTo(target, context with { BypassedByHrManager = false }).IsAllowed;
        Record(target.Id, hired ? ApplicationStatus.Hired : ApplicationStatus.Active, movedByUserId, now,
            usedBypass ? bypassReason!.Trim() : null);

        Raise(new ApplicationStageChanged(Id, CandidateId, current.Id, target.Id, movedByUserId, now,
            usedBypass ? bypassReason!.Trim() : null));
        if (hired)
        {
            ClosedAt = now;
            Raise(new ApplicationHired(Id, PositionId));
        }
    }

    /// <summary>Süreci red veya aday çekilmesiyle sonlandırır; her aşamadan yapılabilir, neden zorunludur.</summary>
    public void Close(ApplicationStatus status, Guid rejectionReasonId, string? note, Guid closedByUserId, TimeProvider clock)
    {
        EnsureActive();
        if (status is not (ApplicationStatus.Rejected or ApplicationStatus.Withdrawn))
            throw new DomainException("Süreç yalnızca red veya aday çekilmesiyle sonlandırılabilir.");
        if (rejectionReasonId == Guid.Empty)
            throw new DomainException("Ret nedeni seçmelisiniz.");

        var now = clock.GetUtcNow();
        Record(StageId, status, closedByUserId, now);
        RejectionReasonId = rejectionReasonId;
        ClosingNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ClosedAt = now;
        Raise(new ApplicationClosed(Id, CandidateId, status, closedByUserId, now));
    }

    public bool CanUndo(TimeProvider clock) => LastChange() is { } last && clock.GetUtcNow() - last.ChangedAt <= UndoWindow;

    /// <summary>Son değişikliği (aşama geçişi veya sonlandırma) 10 dakika içinde geri alır.</summary>
    public void UndoLastChange(Guid undoneByUserId, TimeProvider clock)
    {
        var last = LastChange() ?? throw new DomainException("Geri alınacak bir değişiklik yok.");
        var now = clock.GetUtcNow();
        if (now - last.ChangedAt > UndoWindow)
            throw new DomainException("Geri alma süresi (10 dakika) doldu.");

        last.MarkUndone(now);
        StageId = last.FromStageId;
        Status = last.FromStatus;
        StageEnteredAt = last.PreviousStageEnteredAt;
        RejectionReasonId = null;
        ClosingNote = null;
        ClosedAt = null;

        Raise(new ApplicationChangeUndone(Id, CandidateId, undoneByUserId, now));
        if (last.ToStatus == ApplicationStatus.Hired)
            Raise(new ApplicationHireReverted(Id, PositionId));
    }

    // Yalnızca en son değişiklik geri alınabilir; o da geri alındıysa geri alınacak bir şey kalmaz.
    private ApplicationStageChange? LastChange() =>
        _history.OrderBy(h => h.ChangedAt).LastOrDefault() is { UndoneAt: null } last ? last : null;

    private void Record(
        Guid toStageId, ApplicationStatus toStatus, Guid changedByUserId, DateTimeOffset now, string? bypassReason = null)
    {
        _history.Add(new ApplicationStageChange(
            StageId, toStageId, Status, toStatus, StageEnteredAt, changedByUserId, now, bypassReason));
        if (toStageId != StageId)
            StageEnteredAt = now;
        StageId = toStageId;
        Status = toStatus;
    }

    private void EnsureActive()
    {
        if (Status != ApplicationStatus.Active)
            throw new DomainException("Sonlanmış süreç üzerinde işlem yapılamaz.");
    }
}
