using Recruitment.Domain.Common;

namespace Recruitment.Domain.CvReviews;

public enum CvReviewDecision
{
    Pending,
    Positive,
    Negative,
    OnHold,
}

/// <summary>
/// Adayın CV'sinin bir takım liderine değerlendirmesi için önerilmesi. Aday aynı anda birden çok
/// lidere önerilebilir; her öneri ayrı bir kayıttır. Olumlu ve olumsuz karar kesindir; "beklet"
/// kararı sonradan kesinleştirilebilir.
/// </summary>
public sealed class CvReview : AggregateRoot
{
    public const int TextMaxLength = 2000;

    private CvReview() { }

    public Guid CandidateId { get; private set; }

    /// <summary>Öneri bir pozisyon içinse adayın o pozisyondaki süreci; pozisyonsuz öneride boş.</summary>
    public Guid? ApplicationId { get; private set; }

    /// <summary>Önerinin yapıldığı birim: pozisyonun birimi, pozisyonsuz öneride seçilen birim.</summary>
    public Guid? DepartmentId { get; private set; }
    public Guid ReviewerUserId { get; private set; }
    public Guid SuggestedByUserId { get; private set; }
    public string? SuggestionNote { get; private set; }
    public CvReviewDecision Decision { get; private set; }
    public string? DecisionReason { get; private set; }
    public Guid? RejectionReasonId { get; private set; }
    public DateTimeOffset SuggestedAt { get; private set; }

    /// <summary>Karar için son tarih (öneri + SLA iş günü).</summary>
    public DateTimeOffset DueAt { get; private set; }

    /// <summary>Hâlâ karar yoksa İK'ya gecikme bildirilecek an.</summary>
    public DateTimeOffset EscalateAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }
    public DateTimeOffset? ReminderSentAt { get; private set; }
    public DateTimeOffset? EscalatedAt { get; private set; }

    /// <summary>Hâlâ karar verilebilir mi (bekliyor veya bekletiliyor)?</summary>
    public bool IsOpen => Decision is CvReviewDecision.Pending or CvReviewDecision.OnHold;

    public static CvReview Suggest(
        Guid candidateId,
        Guid? applicationId,
        Guid? departmentId,
        Guid reviewerUserId,
        Guid suggestedByUserId,
        string? note,
        DateTimeOffset dueAt,
        DateTimeOffset escalateAt,
        TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        var review = new CvReview
        {
            CandidateId = candidateId,
            ApplicationId = applicationId,
            DepartmentId = departmentId,
            ReviewerUserId = reviewerUserId,
            SuggestedByUserId = suggestedByUserId,
            SuggestionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Decision = CvReviewDecision.Pending,
            SuggestedAt = now,
            DueAt = dueAt,
            EscalateAt = escalateAt,
        };
        review.Raise(new CvReviewRequested(review.Id, candidateId, reviewerUserId, suggestedByUserId, now));
        return review;
    }

    public bool IsOverdue(TimeProvider clock) => Decision == CvReviewDecision.Pending && clock.GetUtcNow() > DueAt;

    /// <summary>Kararı yalnızca önerinin yapıldığı takım lideri verir.</summary>
    public bool CanBeDecidedBy(Guid userId) => IsOpen && ReviewerUserId == userId;

    public void Decide(CvReviewDecision decision, string reason, Guid? rejectionReasonId, TimeProvider clock)
    {
        if (!IsOpen)
            throw new DomainException("Bu değerlendirme için karar verilmiş; karar değiştirilemez.");
        if (decision == CvReviewDecision.Pending || decision == Decision)
            throw new DomainException("Olumlu, olumsuz veya beklet kararlarından birini seçmelisiniz.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Kararınızın gerekçesini yazmalısınız.");
        if (decision == CvReviewDecision.Negative && rejectionReasonId is null)
            throw new DomainException("Olumsuz karar için ret nedeni seçmelisiniz.");

        var now = clock.GetUtcNow();
        Decision = decision;
        DecisionReason = reason.Trim();
        RejectionReasonId = decision == CvReviewDecision.Negative ? rejectionReasonId : null;
        DecidedAt = now;
        Raise(new CvReviewDecided(Id, CandidateId, decision, ReviewerUserId, now));
    }

    /// <summary>Süresi dolmuş ve henüz hatırlatılmamışsa hatırlatma event'i üretir.</summary>
    public bool RemindIfOverdue(TimeProvider clock)
    {
        if (ReminderSentAt is not null || !IsOverdue(clock))
            return false;

        ReminderSentAt = clock.GetUtcNow();
        Raise(new CvReviewOverdue(Id, ReviewerUserId));
        return true;
    }

    /// <summary>Hatırlatmadan sonra da karar verilmediyse İK'ya gecikme event'i üretir.</summary>
    public bool EscalateIfDue(TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        if (EscalatedAt is not null || Decision != CvReviewDecision.Pending || now <= EscalateAt)
            return false;

        EscalatedAt = now;
        Raise(new CvReviewEscalated(Id, SuggestedByUserId));
        return true;
    }
}
