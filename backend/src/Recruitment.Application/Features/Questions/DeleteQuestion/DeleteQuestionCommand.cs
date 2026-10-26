using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Questions.DeleteQuestion;

/// <summary>Hiç kullanılmamış soruyu siler; kullanılmış soru yalnızca pasif yapılabilir.</summary>
public sealed record DeleteQuestionCommand(Guid Id) : ICommand<Unit>;

internal sealed class DeleteQuestionCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<DeleteQuestionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteQuestionCommand command, CancellationToken ct)
    {
        var question = await db.VisibleQuestions(currentUser).FirstOrDefaultAsync(q => q.Id == command.Id, ct);
        if (question is null)
            return QuestionErrors.NotFound;

        // Pasif şablonlar da sayılır: şablon yeniden açılabilir. Değerlendirme modülü gelince
        // değerlendirmelerde kullanılan sorular da burada sayılacak.
        if (await db.InterviewTemplates.IgnoreQueryFilters().AnyAsync(t => t.Questions.Any(x => x.QuestionId == question.Id), ct))
            return QuestionErrors.InUse;

        db.Questions.Remove(question);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
