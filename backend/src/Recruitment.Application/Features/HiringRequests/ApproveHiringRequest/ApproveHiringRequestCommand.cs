using FluentValidation;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.HiringRequests.ApproveHiringRequest;

public sealed record ApproveHiringRequestCommand(Guid Id, string? Note) : ICommand<Unit>;

internal sealed class ApproveHiringRequestCommandValidator : AbstractValidator<ApproveHiringRequestCommand>
{
    public ApproveHiringRequestCommandValidator() =>
        RuleFor(c => c.Note).MaximumLength(2000).WithMessage("Not en fazla 2000 karakter olabilir.");
}
