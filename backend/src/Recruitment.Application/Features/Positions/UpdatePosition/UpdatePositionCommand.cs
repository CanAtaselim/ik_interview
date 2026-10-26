using FluentValidation;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Positions.UpdatePosition;

public sealed record UpdatePositionCommand(
    Guid Id,
    string Title,
    int Headcount,
    string? JobDescription,
    Guid? ResponsibleHrUserId) : ICommand<Unit>;

internal sealed class UpdatePositionCommandValidator : AbstractValidator<UpdatePositionCommand>
{
    public UpdatePositionCommandValidator()
    {
        RuleFor(c => c.Title).PositionTitle();
        RuleFor(c => c.Headcount).PositionHeadcount();
        RuleFor(c => c.JobDescription).PositionJobDescription();
    }
}
