namespace Recruitment.Domain.HiringRequests;

/// <summary>Talep sahibinin doldurduğu alanlar.</summary>
public sealed record HiringRequestDetails(
    string Title,
    Guid DepartmentId,
    Guid LevelId,
    int Headcount,
    HiringReason Reason,
    string? ReplacedEmployeeName,
    Urgency Urgency,
    DateOnly? ExpectedStartDate,
    IReadOnlyCollection<string> Competencies,
    string? Description);
