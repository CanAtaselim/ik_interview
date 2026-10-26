using Recruitment.Domain.Common;

namespace Recruitment.Domain.CvReviews;

public sealed record CvReviewRequested(
    Guid ReviewId, Guid CandidateId, Guid ReviewerUserId, Guid ActorUserId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record CvReviewDecided(
    Guid ReviewId, Guid CandidateId, CvReviewDecision Decision, Guid ReviewerUserId, DateTimeOffset OccurredAt)
    : IDomainEvent;

/// <summary>Değerlendirme süresi doldu; takım liderine hatırlatma gider.</summary>
public sealed record CvReviewOverdue(Guid ReviewId, Guid ReviewerUserId) : IDomainEvent;

/// <summary>Hatırlatmadan sonra da karar verilmedi; öneren İK'ya gecikme bildirilir.</summary>
public sealed record CvReviewEscalated(Guid ReviewId, Guid SuggestedByUserId) : IDomainEvent;
