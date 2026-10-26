using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Approvals;

namespace Recruitment.Application.Features.HiringRequests.SubmitHiringRequest;

internal sealed class SubmitHiringRequestCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<SubmitHiringRequestCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SubmitHiringRequestCommand command, CancellationToken ct)
    {
        var request = await db.FindWithSteps(command.Id, ct);
        if (request is null)
            return HiringRequestErrors.NotFound;
        if (!request.IsOwner(currentUser))
            return HiringRequestErrors.NotOwner;

        if (!request.IsComplete)
            return HiringRequestErrors.Incomplete;

        var flow = await db.ApprovalFlowDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Kind == ApprovalFlowKind.HiringRequest, ct);

        request.Submit(flow?.ApproverRoles ?? [], clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
