using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Scorecards;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Interviews.GetInterviews;

/// <summary>
/// Mülakatlar (takvim, aday kartı, bekleyen işler). İK hepsini, diğer kullanıcılar mülakatçı
/// olarak atandıklarını görür. Liste sayfalanmaz; bir seferde en fazla <see cref="MaxResults"/> kayıt döner.
/// </summary>
/// <param name="From">Bu andan sonra biten mülakatlar.</param>
/// <param name="To">Bu andan önce başlayan mülakatlar.</param>
/// <param name="Mine">Yalnızca isteği yapanın mülakatçı olduğu mülakatlar.</param>
/// <param name="AwaitingOutcome">Başlangıcı geçmiş, sonucu girilmemiş mülakatlar.</param>
/// <param name="AwaitingMyScorecard">İsteği yapanın değerlendirmesini henüz göndermediği, yapılmış veya saati geçmiş mülakatlar.</param>
public sealed record GetInterviewsQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    Guid? InterviewerUserId,
    Guid? RoomId,
    Guid? CandidateId,
    Guid? PositionId,
    InterviewStatus? Status,
    bool Mine,
    bool AwaitingOutcome,
    bool AwaitingMyScorecard = false) : IQuery<IReadOnlyList<InterviewListItemDto>>
{
    public const int MaxResults = 500;
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(62);
}

internal sealed class GetInterviewsQueryValidator : AbstractValidator<GetInterviewsQuery>
{
    public GetInterviewsQueryValidator() =>
        RuleFor(q => q)
            .Must(q => q.From is null || q.To is null || (q.To > q.From && q.To - q.From <= GetInterviewsQuery.MaxRange))
            .WithName("to")
            .WithMessage("Tarih aralığı en fazla iki ay olabilir ve bitiş başlangıçtan sonra olmalı.");
}

internal sealed class GetInterviewsQueryHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : IQueryHandler<GetInterviewsQuery, IReadOnlyList<InterviewListItemDto>>
{
    public async Task<Result<IReadOnlyList<InterviewListItemDto>>> Handle(GetInterviewsQuery query, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var interviews = db.VisibleInterviews(currentUser).AsNoTracking();

        if (query.From is { } from)
            interviews = interviews.Where(i => i.EndsAt > from);
        if (query.To is { } to)
            interviews = interviews.Where(i => i.StartsAt < to);
        if (query.InterviewerUserId is { } interviewerId)
            interviews = interviews.Where(i => i.Interviewers.Any(x => x.UserId == interviewerId));
        if (query.Mine)
            interviews = interviews.Where(i => i.Interviewers.Any(x => x.UserId == currentUser.UserId));
        if (query.RoomId is { } roomId)
            interviews = interviews.Where(i => i.RoomId == roomId);
        if (query.CandidateId is { } candidateId)
            interviews = interviews.Where(i => i.CandidateId == candidateId);
        if (query.PositionId is { } positionId)
            interviews = interviews.Where(i => i.PositionId == positionId);
        if (query.Status is { } status)
            interviews = interviews.Where(i => i.Status == status);
        if (query.AwaitingOutcome)
            interviews = interviews.Where(i => i.Status == InterviewStatus.Scheduled && i.StartsAt <= now);

        if (query.AwaitingMyScorecard)
        {
            var userId = currentUser.UserId;
            var scorecards = db.Scorecards;
            interviews = interviews
                .Where(i => i.Interviewers.Any(x => x.UserId == userId))
                .DueForScoring(now)
                .Where(i => !scorecards.Any(s =>
                    s.InterviewId == i.Id && s.InterviewerUserId == userId && s.Status == ScorecardStatus.Submitted));
        }

        var items = await interviews.OrderBy(i => i.StartsAt).Take(GetInterviewsQuery.MaxResults).ToListAsync(ct);
        return await db.ToListItems(items, currentUser.UserId, now, ct);
    }
}
