using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Notifications;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Pipeline.Events;

namespace Recruitment.Application.Features.Notifications.EventHandlers;

/// <summary>
/// Süreç olumsuz sonlandığında (<see cref="ApplicationStatus.Rejected"/>) adaya "olumsuz" e-postası gider.
/// Adayın kendi çekilmesinde gitmez. Aynı süreç için tek e-posta üretilir; otomatik gönderim
/// <c>Recruitment:AutoSendRejectionEmail</c> ile kapatılabilir.
/// </summary>
internal sealed class RejectionEmailNotification(
    IAppDbContext db, Notifier notifier, IOptions<RecruitmentOptions> options) : IDomainEventHandler<ApplicationClosed>
{
    public async Task Handle(ApplicationClosed e, CancellationToken ct)
    {
        if (e.Status != ApplicationStatus.Rejected || !options.Value.AutoSendRejectionEmail)
            return;

        var candidate = await db.Candidates.AsNoTracking()
            .Where(c => c.Id == e.CandidateId)
            .Select(c => c.FirstName + " " + c.LastName)
            .FirstOrDefaultAsync(ct);
        var position = await db.JobApplications.AsNoTracking()
            .Where(a => a.Id == e.ApplicationId)
            .Join(db.Positions, a => a.PositionId, p => p.Id, (_, p) => p.Title)
            .FirstOrDefaultAsync(ct);
        if (candidate is null || position is null)
            return;

        await notifier.EmailCandidate(
            e.CandidateId,
            EmailTemplateCodes.RejectionCandidate,
            new Dictionary<string, string?> { ["AdayAdi"] = candidate, ["Pozisyon"] = position },
            $"rejection-candidate:{e.ApplicationId}",
            ct);
    }
}
