using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates;

namespace Recruitment.Application.Features.CvReviews;

public static class CvReviewErrors
{
    public static readonly Error NotFound = Error.NotFound("CvReview.NotFound", "CV değerlendirmesi bulunamadı.");

    public static readonly Error NotReviewer = Error.Forbidden(
        "CvReview.NotReviewer", "Kararı yalnızca CV'nin önerildiği takım lideri verebilir.");

    public static readonly Error ApplicationNotActive = Error.BusinessRule(
        "CvReview.ApplicationNotActive", "Sonlanmış bir süreç için CV önerilemez.");

    public static Error AlreadySuggested(string reviewerName) => Error.Conflict(
        "CvReview.AlreadySuggested",
        $"Bu aday {reviewerName} adlı takım liderine zaten önerilmiş ve kararı bekleniyor.");

    /// <summary>Aday kara listede; İK onaylarsa yine de önerilir ve onay kayda geçer.</summary>
    public static Error BlacklistMatch(IReadOnlyList<Abstractions.BlacklistMatch> matches) => Error.Conflict(
        "CvReview.BlacklistMatch",
        "Bu aday kara listedeki bir kayıtla eşleşiyor. Kaydı kontrol edin; yine de önermek isterseniz onaylayın.",
        CandidateErrors.BlacklistMatchesKey,
        matches);

    public static Error Field(string field, string message) =>
        Error.Validation(new Dictionary<string, string[]> { [field] = [message] });
}
