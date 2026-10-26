using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Scorecards;

internal static class ScorecardQueries
{
    /// <summary>
    /// Değerlendirilmesi gereken mülakatlar: yapılmış veya saati geçmiş (sonucu henüz girilmemiş)
    /// mülakatlar. İptal edilen ve adayın gelmediği mülakatlar değerlendirilmez.
    /// </summary>
    public static IQueryable<Interview> DueForScoring(this IQueryable<Interview> interviews, DateTimeOffset now) =>
        interviews.Where(i => i.Status == InterviewStatus.Completed ||
                              (i.Status == InterviewStatus.Scheduled && i.StartsAt <= now));

    /// <summary>Süreçteki değerlendirilmesi gereken mülakatlarda henüz gönderilmemiş değerlendirme sayısı.</summary>
    public static async Task<int> PendingScorecardCount(
        this IAppDbContext db, Guid applicationId, DateTimeOffset now, CancellationToken ct)
    {
        var interviews = await db.Interviews.AsNoTracking()
            .Where(i => i.ApplicationId == applicationId)
            .DueForScoring(now)
            .ToListAsync(ct);
        if (interviews.Count == 0)
            return 0;

        var submitted = await db.SubmittedPairs([.. interviews.Select(i => i.Id)], ct);
        return interviews.Sum(i => i.InterviewerUserIds.Count(userId => !submitted.Contains((i.Id, userId))));
    }

    /// <summary>Gönderilmiş değerlendirmelerin (mülakat, mülakatçı) çiftleri.</summary>
    public static async Task<HashSet<(Guid InterviewId, Guid UserId)>> SubmittedPairs(
        this IAppDbContext db, IReadOnlyCollection<Guid> interviewIds, CancellationToken ct) =>
        [.. (await db.Scorecards.AsNoTracking()
                .Where(s => interviewIds.Contains(s.InterviewId) && s.Status == ScorecardStatus.Submitted)
                .Select(s => new { s.InterviewId, s.InterviewerUserId })
                .ToListAsync(ct))
            .Select(s => (s.InterviewId, s.InterviewerUserId))];

    /// <summary>
    /// Bağımsız değerlendirme: İK her değerlendirmeyi görür; mülakatçı kendi değerlendirmesini ve
    /// kendisininkini gönderdikten sonra aynı mülakatın gönderilmiş diğer değerlendirmelerini görür.
    /// </summary>
    public static bool CanView(this Scorecard scorecard, ICurrentUser user, bool ownSubmitted) =>
        user.IsHr || scorecard.InterviewerUserId == user.UserId || (ownSubmitted && scorecard.IsSubmitted);

    public static IReadOnlyList<CompetencyScoreDto> CompetencyScores(
        IEnumerable<ScorecardItem> items, IScoreCalculator calculator, IReadOnlyDictionary<Guid, string> names) =>
        [.. items
            .Where(i => i.Score is not null && i.CompetencyId is not null)
            .GroupBy(i => i.CompetencyId!.Value)
            .Select(g => new
            {
                Id = g.Key,
                Score = calculator.Calculate(g.Select(i => new WeightedScore(i.Score!.Value, i.Weight))),
            })
            .Where(x => x.Score is not null)
            .Select(x => new CompetencyScoreDto(x.Id, names.GetValueOrDefault(x.Id, string.Empty), x.Score!.Value))
            .OrderBy(x => x.Name)];

    public static async Task<ScorecardDto> ToDto(
        this Scorecard scorecard,
        IAppDbContext db,
        IScoreCalculator calculator,
        ICurrentUser user,
        DateTimeOffset interviewStartsAt,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var items = scorecard.OrderedItems;
        var names = await db.ItemNames(items.Select(i => i.CompetencyId).OfType<Guid>().Distinct(), ct);
        var interviewerName = await db.Users.Where(u => u.Id == scorecard.InterviewerUserId)
            .Select(u => u.DisplayName).FirstOrDefaultAsync(ct) ?? string.Empty;
        var isOwnDraft = scorecard.InterviewerUserId == user.UserId && !scorecard.IsSubmitted;

        return new ScorecardDto(
            scorecard.Id,
            scorecard.InterviewId,
            scorecard.InterviewerUserId,
            interviewerName,
            scorecard.Status,
            scorecard.Recommendation,
            scorecard.GeneralNote,
            scorecard.SubmittedAt,
            scorecard.ReopenReason,
            scorecard.WeightedScore(calculator),
            CompetencyScores(items, calculator, names),
            [.. items.Select(i => new ScorecardItemDto(
                i.Id, i.QuestionId, i.Text, i.Guidance, i.CompetencyId,
                i.CompetencyId is { } id ? names.GetValueOrDefault(id) : null,
                i.Weight, i.Score, i.Note, i.IsCustom))],
            CanEdit: isOwnDraft,
            CanSubmit: isOwnDraft && now >= interviewStartsAt);
    }
}
