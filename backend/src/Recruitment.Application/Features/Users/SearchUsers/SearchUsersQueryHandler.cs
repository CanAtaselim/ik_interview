using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Users.SearchUsers;

internal sealed class SearchUsersQueryHandler(IAppDbContext db)
    : IQueryHandler<SearchUsersQuery, PagedResult<UserSummaryDto>>
{
    public async Task<Result<PagedResult<UserSummaryDto>>> Handle(SearchUsersQuery query, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking().Where(u => u.IsActive);

        if (query.Roles is { Count: > 0 } roles)
            users = users.Where(u => u.Roles.Any(r => roles.Contains(r.Role)));

        return await users
            .WhereAnyContains(query.Search, u => u.DisplayName, u => u.Username)
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserSummaryDto(u.Id, u.DisplayName, u.Title, u.Department))
            .ToPagedResult(query.Page, query.PageSize, ct);
    }
}
