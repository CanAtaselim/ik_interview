using Recruitment.Domain.Common;

namespace Recruitment.Domain.Scorecards;

public sealed record ScorecardSubmitted(
    Guid ScorecardId, Guid InterviewId, Guid InterviewerUserId, Recommendation Recommendation, DateTimeOffset OccurredAt)
    : IDomainEvent;

/// <summary>İK Yöneticisi gönderilmiş değerlendirmeyi düzeltme için yeniden açtı.</summary>
public sealed record ScorecardReopened(
    Guid ScorecardId, Guid InterviewId, Guid InterviewerUserId, string Reason, Guid ActorUserId, DateTimeOffset OccurredAt)
    : IDomainEvent;
