using FluentValidation;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Positions;

namespace Recruitment.Application.Features.Positions.ChangePositionStatus;

/// <param name="Status">Pozisyonun geçmesi istenen durum.</param>
public sealed record ChangePositionStatusCommand(Guid Id, PositionStatus Status) : ICommand<Unit>;

internal sealed class ChangePositionStatusCommandValidator : AbstractValidator<ChangePositionStatusCommand>
{
    public ChangePositionStatusCommandValidator() =>
        RuleFor(c => c.Status).IsInEnum().WithMessage("Geçerli bir durum seçmelisiniz.");
}
