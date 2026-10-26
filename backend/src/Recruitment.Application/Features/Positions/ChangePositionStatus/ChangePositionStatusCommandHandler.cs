using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Positions;

namespace Recruitment.Application.Features.Positions.ChangePositionStatus;

internal sealed class ChangePositionStatusCommandHandler(IAppDbContext db)
    : ICommandHandler<ChangePositionStatusCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ChangePositionStatusCommand command, CancellationToken ct)
    {
        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == command.Id, ct);
        if (position is null)
            return PositionErrors.NotFound;

        // Geçişin geçerli olup olmadığına pozisyon karar verir.
        switch (command.Status)
        {
            case PositionStatus.Open:
                position.Reopen();
                break;
            case PositionStatus.OnHold:
                position.PutOnHold();
                break;
            case PositionStatus.Closed:
                position.Close();
                break;
            case PositionStatus.Cancelled:
                position.Cancel();
                break;
        }

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
