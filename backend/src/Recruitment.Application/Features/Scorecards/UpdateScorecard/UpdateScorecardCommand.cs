using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Scorecards.UpdateScorecard;

/// <summary>
/// Taslak değerlendirmeyi kaydeder (otomatik kayıt). Listede olmayan sorular çıkarılır,
/// kimliksiz sorular mülakatçının eklediği özel sorulardır.
/// </summary>
public sealed record UpdateScorecardCommand(
    Guid Id, IReadOnlyList<ScorecardItemInput> Items, string? GeneralNote, Recommendation? Recommendation) : ICommand<Unit>;

internal sealed class UpdateScorecardCommandValidator : AbstractValidator<UpdateScorecardCommand>
{
    public UpdateScorecardCommandValidator()
    {
        RuleFor(c => c.Items).NotNull().WithMessage("Sorular gönderilmeli.");
        RuleFor(c => c.GeneralNote)
            .MaximumLength(Scorecard.NoteMaxLength)
            .WithMessage($"Genel not en fazla {Scorecard.NoteMaxLength} karakter olabilir.");
        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Score)
                .InclusiveBetween(ScoreRubric.Min, ScoreRubric.Max)
                .When(i => i.Score is not null)
                .WithMessage($"Puan {ScoreRubric.Min} ile {ScoreRubric.Max} arasında olmalı.");
            item.RuleFor(i => i.Text)
                .MaximumLength(Scorecard.TextMaxLength)
                .WithMessage($"Soru en fazla {Scorecard.TextMaxLength} karakter olabilir.");
            item.RuleFor(i => i.Note)
                .MaximumLength(Scorecard.NoteMaxLength)
                .WithMessage($"Not en fazla {Scorecard.NoteMaxLength} karakter olabilir.");
        });
    }
}

internal sealed class UpdateScorecardCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<UpdateScorecardCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateScorecardCommand command, CancellationToken ct)
    {
        var scorecard = await db.Scorecards.FirstOrDefaultAsync(s => s.Id == command.Id, ct);
        if (scorecard is null)
            return ScorecardErrors.NotFound;
        if (scorecard.InterviewerUserId != currentUser.UserId)
            return ScorecardErrors.NotOwner;

        scorecard.UpdateDraft(command.Items, command.GeneralNote, command.Recommendation);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
