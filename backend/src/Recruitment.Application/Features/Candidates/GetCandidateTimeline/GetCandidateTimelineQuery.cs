using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Candidates.GetCandidateTimeline;

public sealed record GetCandidateTimelineQuery(Guid CandidateId, int Page = 1, int PageSize = 50)
    : IQuery<PagedResult<TimelineEntryDto>>;

internal sealed class GetCandidateTimelineQueryHandler(IAppDbContext db)
    : IQueryHandler<GetCandidateTimelineQuery, PagedResult<TimelineEntryDto>>
{
    public async Task<Result<PagedResult<TimelineEntryDto>>> Handle(GetCandidateTimelineQuery query, CancellationToken ct)
    {
        if (!await db.Candidates.AnyAsync(c => c.Id == query.CandidateId, ct))
            return CandidateErrors.NotFound;

        var users = db.Users;

        return await db.TimelineEntries.AsNoTracking()
            .Where(t => t.CandidateId == query.CandidateId)
            .OrderByDescending(t => t.OccurredAt)
            .ThenByDescending(t => t.Id)
            .Select(t => new TimelineEntryDto(
                t.Id,
                t.Type,
                t.Summary,
                users.Where(u => u.Id == t.ActorUserId).Select(u => u.DisplayName).FirstOrDefault(),
                t.OccurredAt))
            .ToPagedResult(query.Page, query.PageSize, ct);
    }
}
