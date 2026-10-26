using Recruitment.Domain.CvReviews;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Positions;

namespace Recruitment.Application.Features.Pipeline;

public sealed record PipelineStageDto(Guid Id, string Name, StageKind Kind, int Order, bool IsOptional);

/// <param name="CanUndo">Son değişiklik hâlâ geri alınabilir mi (10 dakika)?</param>
/// <param name="CvDecision">
/// Adayın bu süreçteki CV değerlendirmelerinin özeti: olumlu karar varsa olumlu, yoksa bekleyen,
/// yoksa en son karar. Hiç önerilmediyse boş.
/// </param>
/// <param name="NextInterviewAt">Süreçteki en yakın planlı mülakatın başlangıcı.</param>
public sealed record PipelineCardDto(
    Guid ApplicationId,
    Guid CandidateId,
    string CandidateName,
    Guid StageId,
    ApplicationStatus Status,
    DateTimeOffset StageEnteredAt,
    string? ClosingReason,
    bool CanUndo,
    CvReviewDecision? CvDecision,
    DateTimeOffset? NextInterviewAt);

/// <param name="Active">Devam eden ve işe alımla sonuçlanan süreçler (kanban kartları).</param>
/// <param name="Closed">Red veya çekilmeyle sonlananlar.</param>
public sealed record PositionPipelineDto(
    Guid PositionId,
    string PositionTitle,
    PositionStatus PositionStatus,
    int Headcount,
    int FilledCount,
    IReadOnlyList<PipelineStageDto> Stages,
    IReadOnlyList<PipelineCardDto> Active,
    IReadOnlyList<PipelineCardDto> Closed);

public sealed record CandidateApplicationDto(
    Guid Id,
    Guid PositionId,
    string PositionTitle,
    string StageName,
    ApplicationStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset StageEnteredAt,
    string? ClosingReason,
    string? ClosingNote,
    DateTimeOffset? ClosedAt,
    bool CanUndo);
