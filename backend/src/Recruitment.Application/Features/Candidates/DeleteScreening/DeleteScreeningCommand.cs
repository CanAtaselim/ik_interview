using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Candidates.DeleteScreening;

/// <summary>
/// Ön görüşme kaydını aday kartından kaldırır. Kayıt soft delete edilir (KVKK kuralları netleşene kadar
/// hiçbir veri geri dönüşsüz yok edilmez) ve zaman çizelgesine yazılır.
/// </summary>
public sealed record DeleteScreeningCommand(Guid CandidateId, Guid ScreeningId) : ICommand<Unit>;

internal sealed class DeleteScreeningCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<DeleteScreeningCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteScreeningCommand command, CancellationToken ct)
    {
        var candidate = await db.FindCandidate(command.CandidateId, ct);
        if (candidate is null)
            return CandidateErrors.NotFound;
        if (candidate.Screenings.All(s => s.Id != command.ScreeningId))
            return CandidateErrors.ScreeningNotFound;

        candidate.RemoveScreening(command.ScreeningId, currentUser.UserId, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
