using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Questions;

namespace Recruitment.Application.Features.InterviewTemplates.UpdateInterviewTemplate;

/// <summary>
/// Şablonu günceller. Planlanmış mülakatlar şablonun güncel halini gösterir; değerlendirme
/// başladığında sorular o anki halleriyle değerlendirmeye kopyalanır.
/// </summary>
public sealed record UpdateInterviewTemplateCommand(
    Guid Id, string Name, Guid DepartmentId, Guid? LevelId, Guid? InterviewTypeId, IReadOnlyList<Guid> QuestionIds)
    : ICommand<Unit>, IInterviewTemplateFields;

internal sealed class UpdateInterviewTemplateCommandValidator : InterviewTemplateFieldsValidator<UpdateInterviewTemplateCommand>;

internal sealed class UpdateInterviewTemplateCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<UpdateInterviewTemplateCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateInterviewTemplateCommand command, CancellationToken ct)
    {
        var template = await db.VisibleTemplates(currentUser).FirstOrDefaultAsync(t => t.Id == command.Id, ct);
        if (template is null)
            return QuestionErrors.TemplateNotFound;
        if (await db.FindInvalidTemplate(currentUser, command, template.OrderedQuestionIds, ct) is { } invalid)
            return invalid;

        template.Update(command.Name, command.DepartmentId, command.LevelId, command.InterviewTypeId, command.QuestionIds);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
