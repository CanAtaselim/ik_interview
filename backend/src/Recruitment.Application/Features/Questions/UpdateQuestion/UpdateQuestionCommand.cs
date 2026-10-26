using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Questions;

namespace Recruitment.Application.Features.Questions.UpdateQuestion;

/// <summary>Soruyu günceller; içerik değiştiyse yeni sürüm oluşur, eski sürümler saklanır.</summary>
public sealed record UpdateQuestionCommand(
    Guid Id,
    Guid DepartmentId,
    string? PositionTitle,
    Guid? LevelId,
    Guid CompetencyId,
    QuestionType Type,
    int Difficulty,
    decimal Weight,
    string Text,
    string? Guidance) : ICommand<Unit>, IQuestionFields;

internal sealed class UpdateQuestionCommandValidator : QuestionFieldsValidator<UpdateQuestionCommand>;

internal sealed class UpdateQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<UpdateQuestionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateQuestionCommand command, CancellationToken ct)
    {
        var question = await db.VisibleQuestions(currentUser).FirstOrDefaultAsync(q => q.Id == command.Id, ct);
        if (question is null)
            return QuestionErrors.NotFound;
        // Takım lideri soruyu başka bir birime de taşıyamaz.
        if (currentUser.DepartmentError(command.DepartmentId) is { } forbidden)
            return forbidden;
        if (await db.FindInvalidSelections(command, ct) is { } invalid)
            return invalid;

        question.Update(command.ToContent(), currentUser.UserId, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
