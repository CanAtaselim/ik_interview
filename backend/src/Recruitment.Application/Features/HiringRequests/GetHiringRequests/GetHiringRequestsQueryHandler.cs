using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.HiringRequests.GetHiringRequests;

internal sealed class GetHiringRequestsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetHiringRequestsQuery, PagedResult<HiringRequestListItemDto>>
{
    public async Task<Result<PagedResult<HiringRequestListItemDto>>> Handle(
        GetHiringRequestsQuery query, CancellationToken ct)
    {
        var requests = db.HiringRequests.AsNoTracking()
            .Where(HiringRequestAccess.VisibleTo(currentUser).ToExpression());

        if (query.AwaitingMyDecision)
            requests = requests.Where(HiringRequestAccess.AwaitingDecisionBy(currentUser).ToExpression());

        if (query.Status is { } status)
            requests = requests.Where(r => r.Status == status);

        requests = requests.WhereAnyContains(query.Search, r => r.Title);

        var dictionary = db.DictionaryItems;
        var users = db.Users;

        return await requests
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new HiringRequestListItemDto(
                r.Id,
                r.Title,
                dictionary.Where(d => d.Id == r.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? string.Empty,
                dictionary.Where(d => d.Id == r.LevelId).Select(d => d.Name).FirstOrDefault() ?? string.Empty,
                r.Headcount,
                r.Urgency,
                r.Status,
                r.AwaitingRole,
                users.Where(u => u.Id == r.RequestedByUserId).Select(u => u.DisplayName).FirstOrDefault() ?? string.Empty,
                r.CreatedAt))
            .ToPagedResult(query.Page, query.PageSize, ct);
    }
}
