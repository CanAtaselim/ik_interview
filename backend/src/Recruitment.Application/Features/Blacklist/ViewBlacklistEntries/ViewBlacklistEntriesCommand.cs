using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Common;

namespace Recruitment.Application.Features.Blacklist.ViewBlacklistEntries;

/// <summary>
/// Kara liste kayıtlarını listeler. Görüntüleme erişim kaydına yazıldığı için sorgu değil command'dır.
/// </summary>
public sealed record ViewBlacklistEntriesCommand(
    BlacklistStatus? Status,
    string? Search,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : ICommand<PagedResult<BlacklistEntryListItemDto>>;

internal sealed class ViewBlacklistEntriesCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<ViewBlacklistEntriesCommand, PagedResult<BlacklistEntryListItemDto>>
{
    public async Task<Result<PagedResult<BlacklistEntryListItemDto>>> Handle(
        ViewBlacklistEntriesCommand command, CancellationToken ct)
    {
        var entries = db.BlacklistEntries.AsQueryable();
        var candidates = db.Candidates;
        var dictionary = db.DictionaryItems;
        var users = db.Users;

        if (command.Status is { } status)
            entries = entries.Where(e => e.Status == status);

        // Aday adı, kara liste eşleştirmesindeki sadeleştirilmiş adla aranır.
        var name = ContactNameTerm(command.Search);
        if (name.Length > 0)
            entries = entries.Where(e => candidates.Any(c => c.Id == e.CandidateId && c.NormalizedName.Contains(name)));

        var page = await entries
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new BlacklistEntryListItemDto(
                e.Id,
                e.CandidateId,
                candidates.Where(c => c.Id == e.CandidateId).Select(c => c.FirstName + " " + c.LastName).FirstOrDefault() ?? string.Empty,
                dictionary.Where(d => d.Id == e.ReasonId).Select(d => d.Name).FirstOrDefault() ?? string.Empty,
                e.Status,
                e.ExpiresAt,
                users.Where(u => u.Id == e.RequestedByUserId).Select(u => u.DisplayName).FirstOrDefault(),
                e.CreatedAt))
            .ToPagedResult(command.Page, command.PageSize, ct);

        db.AccessLogs.Add(AccessLog.Blacklist(
            AccessAction.BlacklistListViewed, currentUser.UserId, candidateId: null, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);

        return page;
    }

    private static string ContactNameTerm(string? search) => string.Join(' ', SearchText.Tokenize(search));
}
