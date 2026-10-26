using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.HiringRequests.RejectHiringRequest;

internal sealed class RejectHiringRequestCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<RejectHiringRequestCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RejectHiringRequestCommand command, CancellationToken ct)
    {
        var request = await db.FindWithSteps(command.Id, ct);
        if (request is null)
            return HiringRequestErrors.NotFound;
        if (!request.CanBeDecidedBy(currentUser.Roles, currentUser.DepartmentId))
            return HiringRequestErrors.CannotDecide;

        request.Reject(currentUser.UserId, command.Reason, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
