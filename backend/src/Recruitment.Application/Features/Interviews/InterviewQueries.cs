using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Interviews;

namespace Recruitment.Application.Features.Interviews;

internal sealed record InterviewNameLookup(
    IReadOnlyDictionary<Guid, string> Candidates,
    IReadOnlyDictionary<Guid, string> Positions,
    IReadOnlyDictionary<Guid, string> Users,
    IReadOnlyDictionary<Guid, string> Dictionary);

internal static class InterviewQueries
{
    /// <summary>İK tüm mülakatları, diğer kullanıcılar yalnızca mülakatçı olarak atandıklarını görür.</summary>
    public static IQueryable<Interview> VisibleInterviews(this IAppDbContext db, ICurrentUser currentUser)
    {
        if (currentUser.IsHr)
            return db.Interviews;

        var userId = currentUser.UserId;
        return db.Interviews.Where(i => i.Interviewers.Any(x => x.UserId == userId));
    }

    public static async Task<InterviewNameLookup> InterviewNames(
        this IAppDbContext db, IReadOnlyCollection<Interview> interviews, CancellationToken ct, IEnumerable<Guid>? extraUserIds = null)
    {
        var candidateIds = interviews.Select(i => i.CandidateId).Distinct().ToList();
        var positionIds = interviews.Select(i => i.PositionId).Distinct().ToList();
        var userIds = interviews.SelectMany(i => i.InterviewerUserIds).Concat(extraUserIds ?? []).Distinct().ToList();
        var itemIds = interviews.Select(i => i.TypeId)
            .Concat(interviews.Select(i => i.RoomId).OfType<Guid>())
            .Distinct()
            .ToList();

        return new InterviewNameLookup(
            await db.Candidates.AsNoTracking()
                .Where(c => candidateIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.FirstName + " " + c.LastName, ct),
            await db.Positions.AsNoTracking()
                .Where(p => positionIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Title, ct),
            await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct),
            await db.ItemNames(itemIds, ct));
    }

    public static InterviewListItemDto ToListItem(
        this Interview interview, InterviewNameLookup names, Guid currentUserId, DateTimeOffset now) =>
        new(
            interview.Id,
            interview.ApplicationId,
            interview.CandidateId,
            names.Candidates.GetValueOrDefault(interview.CandidateId, string.Empty),
            interview.PositionId,
            names.Positions.GetValueOrDefault(interview.PositionId, string.Empty),
            interview.TypeId,
            names.Dictionary.GetValueOrDefault(interview.TypeId, string.Empty),
            interview.Mode,
            interview.StartsAt,
            interview.EndsAt,
            interview.DurationMinutes,
            interview.RoomId,
            interview.RoomId is { } roomId ? names.Dictionary.GetValueOrDefault(roomId) : null,
            interview.MeetingUrl,
            interview.Status,
            [.. interview.InterviewerUserIds.Select(id => new InterviewerDto(id, names.Users.GetValueOrDefault(id, string.Empty)))],
            interview.IsAssignedTo(currentUserId),
            interview.Status == InterviewStatus.Scheduled && interview.StartsAt <= now,
            interview.TemplateId,
            interview.NotifyCandidate);

    public static async Task<List<InterviewListItemDto>> ToListItems(
        this IAppDbContext db, IReadOnlyCollection<Interview> interviews, Guid currentUserId, DateTimeOffset now, CancellationToken ct)
    {
        var names = await db.InterviewNames(interviews, ct);
        return [.. interviews.Select(i => i.ToListItem(names, currentUserId, now))];
    }
}
