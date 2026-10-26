using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Questions;

namespace Recruitment.Application.Features.Questions.CreateQuestion;

public sealed record CreateQuestionCommand(
    Guid DepartmentId,
    string? PositionTitle,
    Guid? LevelId,
    Guid CompetencyId,
    QuestionType Type,
    int Difficulty,
    decimal Weight,
    string Text,
    string? Guidance) : ICommand<Guid>, IQuestionFields;

internal sealed class CreateQuestionCommandValidator : QuestionFieldsValidator<CreateQuestionCommand>;

internal sealed class CreateQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<CreateQuestionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateQuestionCommand command, CancellationToken ct)
    {
        if (currentUser.DepartmentError(command.DepartmentId) is { } forbidden)
            return forbidden;
        if (await db.FindInvalidSelections(command, ct) is { } invalid)
            return invalid;

        var question = Question.Create(command.ToContent(), currentUser.UserId, clock);
        db.Questions.Add(question);
        await db.SaveChangesAsync(ct);
        return question.Id;
    }
}
