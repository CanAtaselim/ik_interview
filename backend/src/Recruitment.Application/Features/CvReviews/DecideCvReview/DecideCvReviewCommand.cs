using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.CvReviews;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Application.Features.CvReviews.DecideCvReview;

/// <summary>Takım liderinin kendisine önerilen CV için kararı. Olumlu ve olumsuz karar kesindir.</summary>
public sealed record DecideCvReviewCommand(
    Guid ReviewId, CvReviewDecision Decision, string Reason, Guid? RejectionReasonId) : ICommand<Unit>;

internal sealed class DecideCvReviewCommandValidator : AbstractValidator<DecideCvReviewCommand>
{
    public DecideCvReviewCommandValidator()
    {
        RuleFor(c => c.Decision)
            .Must(d => d is CvReviewDecision.Positive or CvReviewDecision.Negative or CvReviewDecision.OnHold)
            .WithMessage("Olumlu, olumsuz veya beklet kararlarından birini seçmelisiniz.");
        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage("Kararınızın gerekçesini yazmalısınız.")
            .MaximumLength(CvReview.TextMaxLength)
            .WithMessage($"Gerekçe en fazla {CvReview.TextMaxLength} karakter olabilir.");
        RuleFor(c => c.RejectionReasonId)
            .NotEmpty()
            .When(c => c.Decision == CvReviewDecision.Negative)
            .WithMessage("Olumsuz karar için ret nedeni seçmelisiniz.");
    }
}

internal sealed class DecideCvReviewCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<DecideCvReviewCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DecideCvReviewCommand command, CancellationToken ct)
    {
        var review = await db.VisibleCvReviews(currentUser).FirstOrDefaultAsync(r => r.Id == command.ReviewId, ct);
        if (review is null)
            return CvReviewErrors.NotFound;
        if (review.ReviewerUserId != currentUser.UserId)
            return CvReviewErrors.NotReviewer;

        if (command.Decision == CvReviewDecision.Negative &&
            !await db.IsSelectable(command.RejectionReasonId!.Value, DictionaryType.RejectionReason, ct))
        {
            return CvReviewErrors.Field("rejectionReasonId", "Geçerli bir ret nedeni seçmelisiniz.");
        }

        review.Decide(command.Decision, command.Reason, command.RejectionReasonId, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
