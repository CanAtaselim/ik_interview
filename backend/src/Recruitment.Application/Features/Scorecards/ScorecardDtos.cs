using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Scorecards;

public sealed record ScorecardItemDto(
    Guid Id,
    Guid? QuestionId,
    string Text,
    string? Guidance,
    Guid? CompetencyId,
    string? CompetencyName,
    decimal Weight,
    int? Score,
    string? Note,
    bool IsCustom);

public sealed record CompetencyScoreDto(Guid CompetencyId, string Name, decimal Score);

/// <param name="CanEdit">İsteği yapan mülakatçı bu taslağı düzenleyebilir mi?</param>
/// <param name="CanSubmit">Mülakat başladı ve değerlendirme taslakta mı?</param>
public sealed record ScorecardDto(
    Guid Id,
    Guid InterviewId,
    Guid InterviewerUserId,
    string InterviewerName,
    ScorecardStatus Status,
    Recommendation? Recommendation,
    string? GeneralNote,
    DateTimeOffset? SubmittedAt,
    string? ReopenReason,
    decimal? WeightedScore,
    IReadOnlyList<CompetencyScoreDto> CompetencyScores,
    IReadOnlyList<ScorecardItemDto> Items,
    bool CanEdit,
    bool CanSubmit);

public enum ScorecardProgress
{
    NotStarted,
    Draft,
    Submitted,
}

/// <summary>
/// Bir mülakatçının değerlendirme durumu. Öneri ve puan yalnızca isteği yapan görebiliyorsa doludur
/// (bağımsız değerlendirme).
/// </summary>
public sealed record InterviewerScorecardDto(
    Guid UserId,
    string DisplayName,
    ScorecardProgress Progress,
    Guid? ScorecardId,
    Recommendation? Recommendation,
    decimal? WeightedScore,
    bool CanView);

public sealed record DebriefDto(DebriefDecision Decision, string Reason, string? DecidedByName, DateTimeOffset DecidedAt);

/// <param name="OthersHidden">İsteği yapan mülakatçı kendi değerlendirmesini göndermediği için diğerlerini göremiyor.</param>
/// <param name="AverageScore">Görülebilen gönderilmiş değerlendirmelerin ağırlıklı puan ortalaması.</param>
public sealed record InterviewScorecardsDto(
    Guid InterviewId,
    IReadOnlyList<InterviewerScorecardDto> Interviewers,
    Guid? MyScorecardId,
    bool OthersHidden,
    decimal? AverageScore,
    IReadOnlyList<CompetencyScoreDto> CompetencyScores,
    DebriefDto? Debrief,
    bool CanDebrief);

/// <param name="Hidden">
/// İsteği yapan kişinin bu adayla ilgili göndermediği bir değerlendirmesi var; puanlar kendi
/// değerlendirmesini gönderene kadar gizlenir.
/// </param>
public sealed record ComparisonRowDto(
    Guid ApplicationId,
    Guid CandidateId,
    string CandidateName,
    string StageName,
    Domain.Pipeline.ApplicationStatus Status,
    int SubmittedCount,
    int ExpectedCount,
    decimal? AverageScore,
    IReadOnlyList<CompetencyScoreDto> CompetencyScores,
    IReadOnlyDictionary<Recommendation, int> Recommendations,
    DebriefDecision? LastDebrief,
    bool Hidden);

public sealed record PositionComparisonDto(
    Guid PositionId, string PositionTitle, IReadOnlyList<CompetencyScoreDto> Competencies, IReadOnlyList<ComparisonRowDto> Candidates);
