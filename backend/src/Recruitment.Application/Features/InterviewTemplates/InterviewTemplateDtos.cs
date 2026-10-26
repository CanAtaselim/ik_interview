using Recruitment.Application.Features.Questions;

namespace Recruitment.Application.Features.InterviewTemplates;

public sealed record InterviewTemplateListItemDto(
    Guid Id,
    string Name,
    Guid DepartmentId,
    string DepartmentName,
    Guid? LevelId,
    string? LevelName,
    Guid? InterviewTypeId,
    string? InterviewTypeName,
    bool IsActive,
    int QuestionCount,
    bool CanManage);

/// <param name="Questions">Şablondaki sorular, sırasıyla.</param>
public sealed record InterviewTemplateDetailDto(
    InterviewTemplateListItemDto Template, IReadOnlyList<QuestionListItemDto> Questions);
