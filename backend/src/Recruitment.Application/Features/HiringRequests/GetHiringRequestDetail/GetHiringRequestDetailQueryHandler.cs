using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;

namespace Recruitment.Application.Features.HiringRequests.GetHiringRequestDetail;

internal sealed class GetHiringRequestDetailQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetHiringRequestDetailQuery, HiringRequestDetailDto>
{
    public async Task<Result<HiringRequestDetailDto>> Handle(GetHiringRequestDetailQuery query, CancellationToken ct)
    {
        // Göremeyeceği talep, kullanıcı için yok hükmündedir (varlığı da sızdırılmaz).
        var request = await db.HiringRequests.AsNoTracking()
            .Include(r => r.ApprovalSteps)
            .Where(HiringRequestAccess.VisibleTo(currentUser).ToExpression())
            .FirstOrDefaultAsync(r => r.Id == query.Id, ct);
        if (request is null)
            return HiringRequestErrors.NotFound;

        var dictionaryNames = await db.ItemNames([request.DepartmentId, request.LevelId], ct);

        var userIds = request.ApprovalSteps
            .Select(s => s.DecidedByUserId)
            .OfType<Guid>()
            .Append(request.RequestedByUserId)
            .ToList();
        var userNames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var positionId = await db.Positions
            .Where(p => p.HiringRequestId == request.Id)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        var steps = request.ApprovalSteps
            .OrderBy(s => s.Order)
            .Select(s => new ApprovalStepDto(
                s.Order,
                s.Role,
                s.Decision,
                s.DecidedByUserId is { } decidedBy ? userNames.GetValueOrDefault(decidedBy) : null,
                s.Note,
                s.DecidedAt))
            .ToList();

        return new HiringRequestDetailDto(
            request.Id,
            request.Title,
            request.DepartmentId,
            dictionaryNames.GetValueOrDefault(request.DepartmentId, string.Empty),
            request.LevelId,
            dictionaryNames.GetValueOrDefault(request.LevelId, string.Empty),
            request.Headcount,
            request.Reason,
            request.ReplacedEmployeeName,
            request.Urgency,
            request.ExpectedStartDate,
            request.Competencies,
            request.Description,
            request.Status,
            request.AwaitingRole,
            userNames.GetValueOrDefault(request.RequestedByUserId, string.Empty),
            request.CreatedAt,
            request.SubmittedAt,
            request.CompletedAt,
            positionId,
            steps,
            request.PermissionsFor(currentUser));
    }
}
