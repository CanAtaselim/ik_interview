using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.HiringRequests;

namespace Recruitment.Application.Features.HiringRequests.UpdateHiringRequest;

public sealed record UpdateHiringRequestCommand(
    Guid Id,
    string Title,
    Guid DepartmentId,
    Guid LevelId,
    int Headcount,
    HiringReason Reason,
    string? ReplacedEmployeeName,
    Urgency Urgency,
    DateOnly? ExpectedStartDate,
    IReadOnlyCollection<string>? Competencies,
    string? Description) : ICommand<Unit>, IHiringRequestFields;

internal sealed class UpdateHiringRequestCommandValidator : HiringRequestFieldsValidator<UpdateHiringRequestCommand>;
