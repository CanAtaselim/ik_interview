using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;

namespace Recruitment.Application.Features.Positions.UpdatePosition;

internal sealed class UpdatePositionCommandHandler(IAppDbContext db) : ICommandHandler<UpdatePositionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdatePositionCommand command, CancellationToken ct)
    {
        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == command.Id, ct);
        if (position is null)
            return PositionErrors.NotFound;
        // Eski serbest metinli adlar olduğu gibi kalabilir; yalnızca değiştirilen ad listeden seçilmeli.
        if (command.Title.Trim() != position.Title && !await db.IsSelectablePositionTitle(command.Title, ct))
            return PositionErrors.InvalidTitle;
        if (await db.FindInvalidResponsibleHr(command.ResponsibleHrUserId, ct) is { } error)
            return error;

        position.UpdateDetails(command.Title, command.Headcount, command.JobDescription, command.ResponsibleHrUserId);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
