using FluentValidation;
using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Features.Positions.CreatePosition;

/// <summary>İK'nın kadro talebi olmadan doğrudan pozisyon açması.</summary>
public sealed record CreatePositionCommand(
    string Title,
    Guid DepartmentId,
    Guid LevelId,
    int Headcount,
    string? JobDescription,
    Guid? ResponsibleHrUserId) : ICommand<Guid>;

internal sealed class CreatePositionCommandValidator : AbstractValidator<CreatePositionCommand>
{
    public CreatePositionCommandValidator()
    {
        RuleFor(c => c.Title).PositionTitle();
        RuleFor(c => c.DepartmentId).NotEmpty().WithMessage("Birim seçmelisiniz.");
        RuleFor(c => c.LevelId).NotEmpty().WithMessage("Seviye seçmelisiniz.");
        RuleFor(c => c.Headcount).PositionHeadcount();
        RuleFor(c => c.JobDescription).PositionJobDescription();
    }
}
