using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates;

namespace Recruitment.Application.Features.Interviews;

public static class InterviewErrors
{
    public const string ConflictsKey = "conflicts";

    public static readonly Error NotFound = Error.NotFound("Interview.NotFound", "Mülakat bulunamadı.");

    public static readonly Error ApplicationNotActive = Error.BusinessRule(
        "Interview.ApplicationNotActive", "Sonlanmış bir süreç için mülakat planlanamaz.");

    /// <summary>Mülakatçı veya oda aynı saatte dolu; İK onaylarsa yine de planlanır.</summary>
    public static Error Conflicts(IReadOnlyList<ScheduleConflictDto> conflicts) => Error.Conflict(
        "Interview.Conflicts",
        "Seçilen saatte çakışma var. Saati değiştirin veya yine de planlamak isterseniz onaylayın.",
        ConflictsKey,
        conflicts);

    public static Error BlacklistMatch(IReadOnlyList<Abstractions.BlacklistMatch> matches) => Error.Conflict(
        "Interview.BlacklistMatch",
        "Bu aday kara listedeki bir kayıtla eşleşiyor. Kaydı kontrol edin; yine de planlamak isterseniz onaylayın.",
        CandidateErrors.BlacklistMatchesKey,
        matches);

    public static Error Field(string field, string message) =>
        Error.Validation(new Dictionary<string, string[]> { [field] = [message] });
}
