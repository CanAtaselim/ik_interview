using Recruitment.Application.Features.Candidates;
using Recruitment.Domain.CvReviews;

namespace Recruitment.Application.Features.CvReviews;

/// <param name="IsOverdue">Karar bekleniyor ve süresi dolmuş.</param>
/// <param name="CanDecide">İsteği yapan kullanıcı bu değerlendirme için karar verebilir mi?</param>
public sealed record CvReviewListItemDto(
    Guid Id,
    Guid CandidateId,
    string CandidateName,
    Guid? ApplicationId,
    Guid? PositionId,
    string? PositionTitle,
    string? DepartmentName,
    string ReviewerName,
    string SuggestedByName,
    string? SuggestionNote,
    CvReviewDecision Decision,
    string? DecisionReason,
    string? RejectionReasonName,
    DateTimeOffset SuggestedAt,
    DateTimeOffset DueAt,
    DateTimeOffset? DecidedAt,
    bool IsOverdue,
    bool CanDecide);

public sealed record CvReviewDetailDto(CvReviewListItemDto Review, RestrictedCandidateDto Candidate);

public sealed record SuggestionResultDto(IReadOnlyList<Guid> ReviewIds);
