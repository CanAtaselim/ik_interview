using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Blacklist;

namespace Recruitment.Application.Features.Candidates.GetCandidateDetail;

internal sealed class GetCandidateDetailQueryHandler(IAppDbContext db)
    : IQueryHandler<GetCandidateDetailQuery, CandidateDetailDto>
{
    public async Task<Result<CandidateDetailDto>> Handle(GetCandidateDetailQuery query, CancellationToken ct)
    {
        var candidate = await db.Candidates.AsNoTracking()
            .Include(c => c.Documents)
            .Include(c => c.Screenings)
            .FirstOrDefaultAsync(c => c.Id == query.Id, ct);
        if (candidate is null)
            return CandidateErrors.NotFound;

        var sourceName = candidate.SourceId is { } sourceId
            ? (await db.ItemNames([sourceId], ct)).GetValueOrDefault(sourceId)
            : null;

        var userIds = candidate.Documents.Select(d => d.UploadedByUserId)
            .Concat(candidate.Screenings.Select(s => s.InterviewerUserId))
            .Distinct()
            .ToList();
        var userNames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var typeNames = await db.ItemNames(candidate.Documents.Select(d => d.TypeId).OfType<Guid>().Distinct(), ct);

        var duplicates = await db.FindDuplicates(candidate.Email, candidate.Phone, candidate.Id, ct);

        var blacklist = await db.BlacklistEntries
            .Where(e => e.CandidateId == candidate.Id &&
                        (e.Status == BlacklistStatus.PendingApproval || e.Status == BlacklistStatus.Active))
            .Select(e => new CandidateBlacklistDto(e.Id, e.Status))
            .FirstOrDefaultAsync(ct);

        return new CandidateDetailDto(
            candidate.Id,
            candidate.FirstName,
            candidate.LastName,
            candidate.Email,
            candidate.Phone,
            candidate.LinkedInUrl,
            candidate.GitHubUrl,
            candidate.City,
            candidate.SourceId,
            sourceName,
            candidate.Tags,
            candidate.Notes,
            candidate.InTalentPool,
            candidate.CreatedAt,
            [.. candidate.Documents
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => d.ToDto(userNames, typeNames))],
            [.. candidate.Screenings.OrderByDescending(s => s.CreatedAt).Select(s => s.ToDto(userNames))],
            duplicates,
            blacklist);
    }
}
