using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Questions.SetQuestionActive;

/// <summary>Pasif soru yeni şablonlara eklenemez; kullanıldığı şablonlarda kalır.</summary>
public sealed record SetQuestionActiveCommand(Guid Id, bool IsActive) : ICommand<Unit>;

internal sealed class SetQuestionActiveCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<SetQuestionActiveCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SetQuestionActiveCommand command, CancellationToken ct)
    {
        var question = await db.VisibleQuestions(currentUser).FirstOrDefaultAsync(q => q.Id == command.Id, ct);
        if (question is null)
            return QuestionErrors.NotFound;

        question.SetActive(command.IsActive);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
