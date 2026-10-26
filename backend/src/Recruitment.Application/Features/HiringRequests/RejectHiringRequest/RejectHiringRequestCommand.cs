using FluentValidation;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.HiringRequests.RejectHiringRequest;

public sealed record RejectHiringRequestCommand(Guid Id, string Reason) : ICommand<Unit>;

internal sealed class RejectHiringRequestCommandValidator : AbstractValidator<RejectHiringRequestCommand>
{
    public RejectHiringRequestCommandValidator() =>
        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage("Ret gerekçesi yazmalısınız.")
            .MaximumLength(2000).WithMessage("Gerekçe en fazla 2000 karakter olabilir.");
}
