using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Questions;

namespace Recruitment.Application.Features.Questions;

internal static class QuestionQueries
{
    /// <summary>Soruları liste satırına çevirir; adlar ve kullanım sayısı aynı sorguda okunur.</summary>
    public static IQueryable<QuestionListItemDto> ToListItems(
        this IQueryable<Question> questions, IAppDbContext db, ICurrentUser currentUser)
    {
        var dictionary = db.DictionaryItems;
        var templates = db.InterviewTemplates;
        var isHr = currentUser.IsHr;
        var ownDepartment = currentUser.DepartmentId;

        return questions.Select(q => new QuestionListItemDto(
            q.Id,
            q.DepartmentId,
            dictionary.Where(d => d.Id == q.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? string.Empty,
            q.PositionTitle,
            q.LevelId,
            dictionary.Where(d => d.Id == q.LevelId).Select(d => d.Name).FirstOrDefault(),
            q.CompetencyId,
            dictionary.Where(d => d.Id == q.CompetencyId).Select(d => d.Name).FirstOrDefault() ?? string.Empty,
            q.Type,
            q.Difficulty,
            q.Weight,
            q.Text,
            q.Guidance,
            q.IsActive,
            q.Version,
            templates.Count(t => t.Questions.Any(x => x.QuestionId == q.Id)),
            isHr || (ownDepartment != null && q.DepartmentId == ownDepartment)));
    }
}
