using Recruitment.Domain.HiringRequests;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.HiringRequests;

public sealed record HiringRequestListItemDto(
    Guid Id,
    string Title,
    string DepartmentName,
    string LevelName,
    int Headcount,
    Urgency Urgency,
    HiringRequestStatus Status,
    Role? AwaitingRole,
    string RequestedByName,
    DateTimeOffset CreatedAt);

public sealed record ApprovalStepDto(
    int Order,
    Role Role,
    ApprovalDecision Decision,
    string? DecidedByName,
    string? Note,
    DateTimeOffset? DecidedAt);

/// <summary>İsteği yapan kullanıcının bu talepte yapabilecekleri; arayüz butonları buna göre gösterir.</summary>
public sealed record HiringRequestPermissions(bool CanEdit, bool CanSubmit, bool CanDecide, bool CanCancel);

public sealed record HiringRequestDetailDto(
    Guid Id,
    string Title,
    Guid DepartmentId,
    string DepartmentName,
    Guid LevelId,
    string LevelName,
    int Headcount,
    HiringReason Reason,
    string? ReplacedEmployeeName,
    Urgency Urgency,
    DateOnly? ExpectedStartDate,
    IReadOnlyList<string> Competencies,
    string? Description,
    HiringRequestStatus Status,
    Role? AwaitingRole,
    string RequestedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? CompletedAt,
    Guid? PositionId,
    IReadOnlyList<ApprovalStepDto> Steps,
    HiringRequestPermissions Permissions);
