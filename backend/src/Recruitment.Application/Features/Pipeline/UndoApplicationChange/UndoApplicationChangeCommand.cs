using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Pipeline.UndoApplicationChange;

/// <summary>Süreçteki son değişikliği (aşama geçişi veya sonlandırma) 10 dakika içinde geri alır.</summary>
public sealed record UndoApplicationChangeCommand(Guid ApplicationId) : ICommand<Unit>;

internal sealed class UndoApplicationChangeCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<UndoApplicationChangeCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UndoApplicationChangeCommand command, CancellationToken ct)
    {
        var application = await db.FindApplication(command.ApplicationId, ct);
        if (application is null)
            return PipelineErrors.NotFound;

        application.UndoLastChange(currentUser.UserId, clock);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
