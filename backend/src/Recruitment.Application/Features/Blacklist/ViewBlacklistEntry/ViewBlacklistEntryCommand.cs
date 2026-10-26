using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Blacklist.ViewBlacklistEntry;

/// <summary>Kaydın ayrıntısını (gerekçe dahil) getirir ve görüntülemeyi erişim kaydına yazar.</summary>
public sealed record ViewBlacklistEntryCommand(Guid Id) : ICommand<BlacklistEntryDetailDto>;

internal sealed class ViewBlacklistEntryCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<ViewBlacklistEntryCommand, BlacklistEntryDetailDto>
{
    public async Task<Result<BlacklistEntryDetailDto>> Handle(ViewBlacklistEntryCommand command, CancellationToken ct)
    {
        var entry = await db.BlacklistEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == command.Id, ct);
        if (entry is null)
            return BlacklistErrors.NotFound;

        var candidateName = await db.Candidates
            .Where(c => c.Id == entry.CandidateId)
            .Select(c => c.FirstName + " " + c.LastName)
            .FirstOrDefaultAsync(ct) ?? string.Empty;
        var reasonName = (await db.ItemNames([entry.ReasonId], ct)).GetValueOrDefault(entry.ReasonId, string.Empty);

        Guid?[] userIds = [entry.RequestedByUserId, entry.ApprovedByUserId, entry.RemovedByUserId];
        var userNames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        string? NameOf(Guid? userId) => userId is { } id ? userNames.GetValueOrDefault(id) : null;

        db.AccessLogs.Add(AccessLog.Blacklist(
            AccessAction.BlacklistEntryViewed, currentUser.UserId, entry.CandidateId, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);

        var isHrManager = currentUser.IsInRole(Role.HrManager);
        var isOpen = entry.Status is BlacklistStatus.PendingApproval or BlacklistStatus.Active;

        return new BlacklistEntryDetailDto(
            entry.Id,
            entry.CandidateId,
            candidateName,
            reasonName,
            entry.Description,
            entry.Status,
            entry.ExpiresAt,
            NameOf(entry.RequestedByUserId),
            entry.CreatedAt,
            NameOf(entry.ApprovedByUserId),
            entry.ApprovedAt,
            NameOf(entry.RemovedByUserId),
            entry.RemovedAt,
            entry.RemovalReason,
            CanApprove: isHrManager && entry.Status == BlacklistStatus.PendingApproval,
            CanRemove: isHrManager && isOpen);
    }
}
