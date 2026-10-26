using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Questions;

namespace Recruitment.Application.Features.InterviewTemplates.CreateInterviewTemplate;

public sealed record CreateInterviewTemplateCommand(
    string Name, Guid DepartmentId, Guid? LevelId, Guid? InterviewTypeId, IReadOnlyList<Guid> QuestionIds)
    : ICommand<Guid>, IInterviewTemplateFields;

internal sealed class CreateInterviewTemplateCommandValidator : InterviewTemplateFieldsValidator<CreateInterviewTemplateCommand>;

internal sealed class CreateInterviewTemplateCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<CreateInterviewTemplateCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateInterviewTemplateCommand command, CancellationToken ct)
    {
        if (await db.FindInvalidTemplate(currentUser, command, [], ct) is { } invalid)
            return invalid;

        var template = InterviewTemplate.Create(
            command.Name, command.DepartmentId, command.LevelId, command.InterviewTypeId, command.QuestionIds);
        db.InterviewTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return template.Id;
    }
}
