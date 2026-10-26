using Recruitment.Domain.Questions;

namespace Recruitment.Application.Features.Questions;

/// <param name="TemplateCount">Sorunun kullanıldığı şablon sayısı (kullanılan soru silinemez).</param>
/// <param name="CanManage">İsteği yapan kullanıcı soruyu düzenleyebilir mi?</param>
public sealed record QuestionListItemDto(
    Guid Id,
    Guid DepartmentId,
    string DepartmentName,
    string? PositionTitle,
    Guid? LevelId,
    string? LevelName,
    Guid CompetencyId,
    string CompetencyName,
    QuestionType Type,
    int Difficulty,
    decimal Weight,
    string Text,
    string? Guidance,
    bool IsActive,
    int Version,
    int TemplateCount,
    bool CanManage);

public sealed record QuestionVersionDto(
    int Number, string Text, string? Guidance, QuestionType Type, int Difficulty, decimal Weight,
    string CompetencyName, string? CreatedByName, DateTimeOffset CreatedAt);

public sealed record QuestionDetailDto(QuestionListItemDto Question, IReadOnlyList<QuestionVersionDto> Versions);
