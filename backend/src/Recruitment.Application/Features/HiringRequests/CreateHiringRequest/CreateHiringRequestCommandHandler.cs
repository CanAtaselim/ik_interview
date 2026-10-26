using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.HiringRequests;

namespace Recruitment.Application.Features.HiringRequests.CreateHiringRequest;

internal sealed class CreateHiringRequestCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<CreateHiringRequestCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateHiringRequestCommand command, CancellationToken ct)
    {
        if (await db.FindInvalidSelection(command, ct) is { } error)
            return error;

        var request = HiringRequest.Create(command.ToDetails(), currentUser.UserId);

        db.HiringRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request.Id;
    }
}
