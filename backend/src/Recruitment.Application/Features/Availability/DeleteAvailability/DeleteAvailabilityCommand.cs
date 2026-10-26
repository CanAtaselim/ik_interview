using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Availability.DeleteAvailability;

/// <summary>Kullanıcı kendi müsaitlik aralığını kaldırır; planlanmış mülakatları etkilemez.</summary>
public sealed record DeleteAvailabilityCommand(Guid Id) : ICommand<Unit>;

internal sealed class DeleteAvailabilityCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<DeleteAvailabilityCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteAvailabilityCommand command, CancellationToken ct)
    {
        // Başkasının kaydı "bulunamadı" sayılır; varlığı bile açığa çıkmaz.
        var availability = await db.InterviewerAvailabilities
            .FirstOrDefaultAsync(a => a.Id == command.Id && a.UserId == currentUser.UserId, ct);
        if (availability is null)
            return Error.NotFound("Availability.NotFound", "Müsaitlik kaydı bulunamadı.");

        db.InterviewerAvailabilities.Remove(availability);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
