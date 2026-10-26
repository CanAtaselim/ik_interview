using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Positions.GetPositions;

internal sealed class GetPositionsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetPositionsQuery, PagedResult<PositionListItemDto>>
{
    public async Task<Result<PagedResult<PositionListItemDto>>> Handle(GetPositionsQuery query, CancellationToken ct)
    {
        var positions = db.Positions.AsNoTracking().Where(PositionChecks.VisibleTo(currentUser).ToExpression());

        if (query.Status is { } status)
            positions = positions.Where(p => p.Status == status);

        positions = positions.WhereAnyContains(query.Search, p => p.Title);

        var dictionary = db.DictionaryItems;
        var users = db.Users;

        return await positions
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PositionListItemDto(
                p.Id,
                p.Title,
                dictionary.Where(d => d.Id == p.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? string.Empty,
                dictionary.Where(d => d.Id == p.LevelId).Select(d => d.Name).FirstOrDefault() ?? string.Empty,
                p.Headcount,
                p.FilledCount,
                p.Status,
                users.Where(u => u.Id == p.ResponsibleHrUserId).Select(u => u.DisplayName).FirstOrDefault(),
                p.CreatedAt))
            .ToPagedResult(query.Page, query.PageSize, ct);
    }
}
