using Recruitment.Application.Abstractions;
using Recruitment.Domain.HiringRequests;

namespace Recruitment.Application.Features.HiringRequests.CreateHiringRequest;

/// <summary>Talebi taslak olarak oluşturur; onaya ayrıca gönderilir.</summary>
public sealed record CreateHiringRequestCommand(
    string Title,
    Guid DepartmentId,
    Guid LevelId,
    int Headcount,
    HiringReason Reason,
    string? ReplacedEmployeeName,
    Urgency Urgency,
    DateOnly? ExpectedStartDate,
    IReadOnlyCollection<string>? Competencies,
    string? Description) : ICommand<Guid>, IHiringRequestFields;

internal sealed class CreateHiringRequestCommandValidator : HiringRequestFieldsValidator<CreateHiringRequestCommand>;
