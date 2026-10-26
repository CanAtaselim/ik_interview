using Recruitment.Domain.Positions;

namespace Recruitment.Application.Features.Positions;

public sealed record PositionListItemDto(
    Guid Id,
    string Title,
    string DepartmentName,
    string LevelName,
    int Headcount,
    int FilledCount,
    PositionStatus Status,
    string? ResponsibleHrName,
    DateTimeOffset CreatedAt);

/// <param name="CanManage">İsteği yapan kullanıcı pozisyonu düzenleyip durumunu değiştirebilir mi?</param>
public sealed record PositionDetailDto(
    Guid Id,
    string Title,
    Guid DepartmentId,
    string DepartmentName,
    Guid LevelId,
    string LevelName,
    int Headcount,
    int FilledCount,
    PositionStatus Status,
    Guid? ResponsibleHrUserId,
    string? ResponsibleHrName,
    string? JobDescription,
    Guid? HiringRequestId,
    DateTimeOffset CreatedAt,
    bool CanManage,
    IReadOnlyList<PositionTemplateDto> InterviewTemplates);

public sealed record PositionTemplateDto(Guid Id, string Name, string? InterviewTypeName, bool IsActive);
