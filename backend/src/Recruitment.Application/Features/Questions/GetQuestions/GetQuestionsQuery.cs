using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Questions;

namespace Recruitment.Application.Features.Questions.GetQuestions;

/// <summary>Soru bankası. İK tüm birimleri, takım lideri kendi birimini görür.</summary>
public sealed record GetQuestionsQuery(
    string? Search,
    Guid? DepartmentId,
    Guid? CompetencyId,
    Guid? LevelId,
    QuestionType? Type,
    int? Difficulty,
    bool? IsActive,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<QuestionListItemDto>>;

internal sealed class GetQuestionsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetQuestionsQuery, PagedResult<QuestionListItemDto>>
{
    public async Task<Result<PagedResult<QuestionListItemDto>>> Handle(GetQuestionsQuery query, CancellationToken ct)
    {
        var questions = db.VisibleQuestions(currentUser);

        if (query.DepartmentId is { } departmentId)
            questions = questions.Where(q => q.DepartmentId == departmentId);
        if (query.CompetencyId is { } competencyId)
            questions = questions.Where(q => q.CompetencyId == competencyId);
        // Seviye seçilirse o seviyeye ve tüm seviyelere yönelik sorular gelir.
        if (query.LevelId is { } levelId)
            questions = questions.Where(q => q.LevelId == null || q.LevelId == levelId);
        if (query.Type is { } type)
            questions = questions.Where(q => q.Type == type);
        if (query.Difficulty is { } difficulty)
            questions = questions.Where(q => q.Difficulty == difficulty);
        if (query.IsActive is { } isActive)
            questions = questions.Where(q => q.IsActive == isActive);

        return await questions
            .WhereAnyContains(query.Search, q => q.Text, q => q.PositionTitle ?? string.Empty)
            .OrderByDescending(q => q.IsActive)
            .ThenByDescending(q => q.UpdatedAt ?? q.CreatedAt)
            .ToListItems(db, currentUser)
            .ToPagedResult(query.Page, query.PageSize, ct);
    }
}
