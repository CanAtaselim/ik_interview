using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.HiringRequests;
using Recruitment.Domain.HiringRequests.Events;
using Recruitment.Domain.Notifications;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Notifications.EventHandlers;

/// <summary>
/// Kadro talebinde sıradaki onaycıya, sonuçta talep sahibine bildirim. Red, talebi daha önce
/// onaylayanlara da bildirilir.
/// </summary>
internal sealed class HiringRequestNotifications(IAppDbContext db, Notifier notifier) :
    IDomainEventHandler<HiringRequestAwaitingApproval>,
    IDomainEventHandler<HiringRequestApproved>,
    IDomainEventHandler<HiringRequestRejected>
{
    public async Task Handle(HiringRequestAwaitingApproval e, CancellationToken ct)
    {
        var request = await Load(e.HiringRequestId, ct);
        // Departman yöneticisi yalnızca kendi biriminin talebini onaylar.
        var approvers = await notifier.UsersInRole(
            e.Role, e.Role == Role.DepartmentManager ? request.DepartmentId : null, ct);
        var requester = await db.Users.Where(u => u.Id == request.RequestedByUserId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
        var department = (await db.ItemNames([request.DepartmentId], ct)).GetValueOrDefault(request.DepartmentId);

        await notifier.Notify(
            approvers,
            NotificationType.HiringRequestAwaitingApproval,
            $"Onayınızı bekleyen kadro talebi: {request.Title}",
            $"{requester} · {department} · {request.Headcount} kişi",
            Link(request.Id),
            $"hr-await:{request.Id}:{e.Step}",
            ct);
    }

    public async Task Handle(HiringRequestApproved e, CancellationToken ct)
    {
        var request = await Load(e.HiringRequestId, ct);
        await notifier.Notify(
            [request.RequestedByUserId],
            NotificationType.HiringRequestDecided,
            $"Kadro talebiniz onaylandı: {request.Title}",
            "Talep tüm onaylardan geçti; pozisyon açıldı.",
            Link(request.Id),
            $"hr-approved:{request.Id}",
            ct);
    }

    public async Task Handle(HiringRequestRejected e, CancellationToken ct)
    {
        var request = await Load(e.HiringRequestId, ct);
        var rejection = request.ApprovalSteps.FirstOrDefault(s => s.Decision == ApprovalDecision.Rejected);
        var note = rejection?.Note;
        await notifier.Notify(
            [request.RequestedByUserId],
            NotificationType.HiringRequestDecided,
            $"Kadro talebiniz reddedildi: {request.Title}",
            note is null ? null : $"Gerekçe: {note}",
            Link(request.Id),
            $"hr-rejected:{request.Id}",
            ct);

        // Talebi daha önce onaylayan herkes (ör. birim müdürü, İK reddettiyse) sonucu öğrenir.
        // Reddeden kişi kendi kararını zaten bilir; talep sahibine yukarıda bildirildi.
        var earlierApprovers = request.ApprovalSteps
            .Where(s => s.Decision == ApprovalDecision.Approved && s.DecidedByUserId is not null)
            .Select(s => s.DecidedByUserId!.Value)
            .Where(id => id != request.RequestedByUserId && id != rejection?.DecidedByUserId)
            .Distinct()
            .ToList();
        if (earlierApprovers.Count == 0)
            return;

        var rejectedBy = rejection?.DecidedByUserId is { } rejecterId
            ? await db.Users.Where(u => u.Id == rejecterId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct)
            : null;
        var details = new[] { rejectedBy is null ? null : $"Reddeden: {rejectedBy}", note is null ? null : $"Gerekçe: {note}" }
            .Where(part => part is not null);
        await notifier.Notify(
            earlierApprovers,
            NotificationType.HiringRequestDecided,
            $"Onayladığınız kadro talebi reddedildi: {request.Title}",
            string.Join(" · ", details) is { Length: > 0 } text ? text : null,
            Link(request.Id),
            $"hr-rejected-approver:{request.Id}",
            ct);
    }

    private Task<HiringRequest> Load(Guid id, CancellationToken ct) =>
        db.HiringRequests.AsNoTracking().Include(r => r.ApprovalSteps).FirstAsync(r => r.Id == id, ct);

    private static string Link(Guid id) => $"/hiring-requests?selected={id}";
}
