using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Questions;

namespace Recruitment.Application.Features.InterviewTemplates.SetInterviewTemplateActive;

/// <summary>Pasif şablon yeni mülakatlara bağlanmaz; bağlı olduğu mülakatlarda kalır.</summary>
public sealed record SetInterviewTemplateActiveCommand(Guid Id, bool IsActive) : ICommand<Unit>;

internal sealed class SetInterviewTemplateActiveCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<SetInterviewTemplateActiveCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SetInterviewTemplateActiveCommand command, CancellationToken ct)
    {
        var template = await db.VisibleTemplates(currentUser).FirstOrDefaultAsync(t => t.Id == command.Id, ct);
        if (template is null)
            return QuestionErrors.TemplateNotFound;

        template.SetActive(command.IsActive);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
