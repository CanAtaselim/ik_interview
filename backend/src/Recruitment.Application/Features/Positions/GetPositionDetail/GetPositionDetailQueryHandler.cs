using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;

namespace Recruitment.Application.Features.Positions.GetPositionDetail;

internal sealed class GetPositionDetailQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetPositionDetailQuery, PositionDetailDto>
{
    public async Task<Result<PositionDetailDto>> Handle(GetPositionDetailQuery query, CancellationToken ct)
    {
        var position = await db.Positions.AsNoTracking()
            .Where(PositionChecks.VisibleTo(currentUser).ToExpression())
            .FirstOrDefaultAsync(p => p.Id == query.Id, ct);
        if (position is null)
            return PositionErrors.NotFound;

        var dictionaryNames = await db.ItemNames([position.DepartmentId, position.LevelId], ct);
        var responsibleHrName = position.ResponsibleHrUserId is { } hrUserId
            ? await db.Users.Where(u => u.Id == hrUserId).Select(u => u.DisplayName).FirstOrDefaultAsync(ct)
            : null;

        var templateIds = position.InterviewTemplateIds.ToList();
        var dictionary = db.DictionaryItems;
        var templates = await db.InterviewTemplates.AsNoTracking()
            .Where(t => templateIds.Contains(t.Id))
            .Select(t => new PositionTemplateDto(
                t.Id,
                t.Name,
                dictionary.Where(d => d.Id == t.InterviewTypeId).Select(d => d.Name).FirstOrDefault(),
                t.IsActive))
            .ToListAsync(ct);

        return new PositionDetailDto(
            position.Id,
            position.Title,
            position.DepartmentId,
            dictionaryNames.GetValueOrDefault(position.DepartmentId, string.Empty),
            position.LevelId,
            dictionaryNames.GetValueOrDefault(position.LevelId, string.Empty),
            position.Headcount,
            position.FilledCount,
            position.Status,
            position.ResponsibleHrUserId,
            responsibleHrName,
            position.JobDescription,
            position.HiringRequestId,
            position.CreatedAt,
            CanManage: currentUser.IsHr,
            [.. templateIds.Select(id => templates.FirstOrDefault(t => t.Id == id)).OfType<PositionTemplateDto>()]);
    }
}
