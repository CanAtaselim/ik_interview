using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;

namespace Recruitment.Application.Features.Questions.GetQuestionDetail;

/// <summary>Soru ve sürüm geçmişi (en yeni sürüm başta).</summary>
public sealed record GetQuestionDetailQuery(Guid Id) : IQuery<QuestionDetailDto>;

internal sealed class GetQuestionDetailQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetQuestionDetailQuery, QuestionDetailDto>
{
    public async Task<Result<QuestionDetailDto>> Handle(GetQuestionDetailQuery query, CancellationToken ct)
    {
        var item = await db.VisibleQuestions(currentUser)
            .Where(q => q.Id == query.Id)
            .ToListItems(db, currentUser)
            .FirstOrDefaultAsync(ct);
        if (item is null)
            return QuestionErrors.NotFound;

        var versions = await db.Questions.AsNoTracking()
            .Where(q => q.Id == query.Id)
            .SelectMany(q => q.Versions)
            .ToListAsync(ct);
        var userIds = versions.Select(v => v.CreatedByUserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var competencies = await db.ItemNames(versions.Select(v => v.CompetencyId).Distinct(), ct);

        return new QuestionDetailDto(
            item,
            [.. versions
                .OrderByDescending(v => v.Number)
                .Select(v => new QuestionVersionDto(
                    v.Number, v.Text, v.Guidance, v.Type, v.Difficulty, v.Weight,
                    competencies.GetValueOrDefault(v.CompetencyId, string.Empty),
                    users.GetValueOrDefault(v.CreatedByUserId), v.CreatedAt))]);
    }
}
