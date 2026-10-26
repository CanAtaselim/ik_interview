using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Positions;

namespace Recruitment.Application.Features.Positions.CreatePosition;

internal sealed class CreatePositionCommandHandler(IAppDbContext db) : ICommandHandler<CreatePositionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreatePositionCommand command, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (!await db.IsSelectable(command.DepartmentId, DictionaryType.Department, ct))
            errors["departmentId"] = ["Geçerli bir birim seçmelisiniz."];
        if (!await db.IsSelectable(command.LevelId, DictionaryType.Level, ct))
            errors["levelId"] = ["Geçerli bir seviye seçmelisiniz."];
        if (errors.Count > 0)
            return Error.Validation(errors);

        if (await db.FindInvalidResponsibleHr(command.ResponsibleHrUserId, ct) is { } error)
            return error;

        var position = Position.Open(
            command.Title,
            command.DepartmentId,
            command.LevelId,
            command.Headcount,
            command.JobDescription,
            command.ResponsibleHrUserId);

        db.Positions.Add(position);
        await db.SaveChangesAsync(ct);
        return position.Id;
    }
}
