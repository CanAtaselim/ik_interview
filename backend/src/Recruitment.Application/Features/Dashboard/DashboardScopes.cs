using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.CvReviews;
using Recruitment.Domain.HiringRequests;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Positions;

namespace Recruitment.Application.Features.Dashboard;

/// <summary>Dashboard sorgularının ortak filtreleri: pozisyon seçiliyse yalnız o pozisyon, değilse birim.</summary>
internal static class DashboardScopes
{
    public static IQueryable<Position> ScopedPositions(this IAppDbContext db, DashboardFilter filter)
    {
        var positions = db.Positions.AsNoTracking();
        if (filter.PositionId is { } positionId)
            return positions.Where(p => p.Id == positionId);

        return filter.DepartmentId is { } departmentId ? positions.Where(p => p.DepartmentId == departmentId) : positions;
    }

    public static IQueryable<JobApplication> ScopedApplications(this IAppDbContext db, DashboardFilter filter)
    {
        var applications = db.JobApplications.AsNoTracking();
        if (filter.PositionId is { } positionId)
            return applications.Where(a => a.PositionId == positionId);

        return filter.DepartmentId is { } departmentId
            ? applications.Where(a => db.Positions.Any(p => p.Id == a.PositionId && p.DepartmentId == departmentId))
            : applications;
    }

    public static IQueryable<Interview> ScopedInterviews(this IAppDbContext db, DashboardFilter filter)
    {
        var interviews = db.Interviews.AsNoTracking();
        if (filter.PositionId is { } positionId)
            return interviews.Where(i => i.PositionId == positionId);

        return filter.DepartmentId is { } departmentId
            ? interviews.Where(i => db.Positions.Any(p => p.Id == i.PositionId && p.DepartmentId == departmentId))
            : interviews;
    }

    public static IQueryable<HiringRequest> ScopedRequests(this IAppDbContext db, DashboardFilter filter)
    {
        var requests = db.HiringRequests.AsNoTracking();
        if (filter.PositionId is { } positionId)
            return requests.Where(r => db.Positions.Any(p => p.Id == positionId && p.HiringRequestId == r.Id));

        return filter.DepartmentId is { } departmentId ? requests.Where(r => r.DepartmentId == departmentId) : requests;
    }

    /// <summary>Kullanıcı kimliklerinin görünen adları; bulunamayana "Belirtilmemiş" yazılır.</summary>
    public static async Task<Func<Guid, string>> UserNames(
        this IAppDbContext db, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        return id => names.GetValueOrDefault(id, DashboardLabels.Unspecified);
    }

    /// <summary>CV değerlendirmelerinde pozisyon, süreç (başvuru) üzerinden bağlanır.</summary>
    public static IQueryable<CvReview> ScopedCvReviews(this IAppDbContext db, DashboardFilter filter)
    {
        var reviews = db.CvReviews.AsNoTracking();
        if (filter.PositionId is { } positionId)
            return reviews.Where(r => db.JobApplications.Any(a => a.Id == r.ApplicationId && a.PositionId == positionId));

        return filter.DepartmentId is { } departmentId
            ? reviews.Where(r => r.DepartmentId == departmentId ||
                                 db.JobApplications.Any(a => a.Id == r.ApplicationId &&
                                                             db.Positions.Any(p => p.Id == a.PositionId && p.DepartmentId == departmentId)))
            : reviews;
    }
}
