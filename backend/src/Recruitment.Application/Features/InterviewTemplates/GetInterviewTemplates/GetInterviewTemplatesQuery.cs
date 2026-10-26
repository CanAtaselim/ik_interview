using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Questions;

namespace Recruitment.Application.Features.InterviewTemplates.GetInterviewTemplates;

/// <summary>Mülakat şablonları (sayıları az olduğu için sayfalanmaz).</summary>
public sealed record GetInterviewTemplatesQuery(Guid? DepartmentId, bool? IsActive, string? Search)
    : IQuery<IReadOnlyList<InterviewTemplateListItemDto>>;

internal sealed class GetInterviewTemplatesQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetInterviewTemplatesQuery, IReadOnlyList<InterviewTemplateListItemDto>>
{
    public async Task<Result<IReadOnlyList<InterviewTemplateListItemDto>>> Handle(
        GetInterviewTemplatesQuery query, CancellationToken ct)
    {
        var templates = db.VisibleTemplates(currentUser);
        if (query.DepartmentId is { } departmentId)
            templates = templates.Where(t => t.DepartmentId == departmentId);
        if (query.IsActive is { } isActive)
            templates = templates.Where(t => t.IsActive == isActive);

        return await templates
            .WhereAnyContains(query.Search, t => t.Name)
            .OrderByDescending(t => t.IsActive)
            .ThenBy(t => t.Name)
            .ToListItems(db, currentUser)
            .ToListAsync(ct);
    }
}
