using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.HiringRequests.CancelHiringRequest;

internal sealed class CancelHiringRequestCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<CancelHiringRequestCommand, Unit>
{
    public async Task<Result<Unit>> Handle(CancelHiringRequestCommand command, CancellationToken ct)
    {
        // Onay adımları yüklenmeli: iptal kuralı bir onaycının karar verip vermediğine bakar.
        var request = await db.FindWithSteps(command.Id, ct);
        if (request is null)
            return HiringRequestErrors.NotFound;
        if (!request.IsOwner(currentUser) && !currentUser.IsInRole(Role.HrManager))
            return HiringRequestErrors.CannotCancel;

        request.Cancel(clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
