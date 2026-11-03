using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Notifications;
using Recruitment.Domain.Notifications;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.Pipeline.SendOnHoldEmail;

/// <summary>
/// İK'nın, devam eden bir süreçteki adaya "süreciniz devam ediyor" e-postasını elle göndermesi.
/// Her tıklama bir e-posta üretir; adayın e-postası yoksa veya süreç devam etmiyorsa reddedilir.
/// </summary>
public sealed record SendOnHoldEmailCommand(Guid ApplicationId) : ICommand<Unit>;

internal sealed class SendOnHoldEmailCommandHandler(IAppDbContext db, Notifier notifier)
    : ICommandHandler<SendOnHoldEmailCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SendOnHoldEmailCommand command, CancellationToken ct)
    {
        var application = await db.JobApplications.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, ct);
        if (application is null)
            return PipelineErrors.NotFound;
        if (application.Status != ApplicationStatus.Active)
            return PipelineErrors.OnHoldMailNeedsActiveProcess;

        var candidate = await db.Candidates.AsNoTracking()
            .Where(c => c.Id == application.CandidateId)
            .Select(c => new { c.Email, Name = c.FirstName + " " + c.LastName })
            .FirstAsync(ct);
        if (string.IsNullOrWhiteSpace(candidate.Email))
            return PipelineErrors.CandidateHasNoEmail;

        var position = await db.Positions.AsNoTracking()
            .Where(p => p.Id == application.PositionId)
            .Select(p => p.Title)
            .FirstAsync(ct);

        await notifier.EmailCandidate(
            application.CandidateId,
            EmailTemplateCodes.OnHoldCandidate,
            new Dictionary<string, string?> { ["AdayAdi"] = candidate.Name, ["Pozisyon"] = position },
            $"on-hold-candidate:{application.Id}:{Guid.NewGuid():N}",
            ct);
        return Unit.Value;
    }
}
