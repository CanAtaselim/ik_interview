using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.HiringRequests.ApproveHiringRequest;

internal sealed class ApproveHiringRequestCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<ApproveHiringRequestCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ApproveHiringRequestCommand command, CancellationToken ct)
    {
        var request = await db.FindWithSteps(command.Id, ct);
        if (request is null)
            return HiringRequestErrors.NotFound;
        if (!request.CanBeDecidedBy(currentUser.Roles, currentUser.DepartmentId))
            return HiringRequestErrors.CannotDecide;

        request.Approve(currentUser.UserId, command.Note, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
