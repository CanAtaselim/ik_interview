using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.HiringRequests.UpdateHiringRequest;

internal sealed class UpdateHiringRequestCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<UpdateHiringRequestCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateHiringRequestCommand command, CancellationToken ct)
    {
        var request = await db.HiringRequests.FirstOrDefaultAsync(r => r.Id == command.Id, ct);
        if (request is null)
            return HiringRequestErrors.NotFound;
        if (!request.IsOwner(currentUser))
            return HiringRequestErrors.NotOwner;
        if (await db.FindInvalidSelection(command, ct) is { } error)
            return error;

        request.Update(command.ToDetails());
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
