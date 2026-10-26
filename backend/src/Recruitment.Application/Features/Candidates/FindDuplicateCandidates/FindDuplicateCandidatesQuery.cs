using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Candidates.FindDuplicateCandidates;

/// <summary>Form doldurulurken, yazılan e-posta veya telefonla kayıtlı aday var mı diye bakar.</summary>
public sealed record FindDuplicateCandidatesQuery(string? Email, string? Phone, Guid? ExcludeId)
    : IQuery<IReadOnlyList<DuplicateCandidateDto>>;

internal sealed class FindDuplicateCandidatesQueryHandler(IAppDbContext db)
    : IQueryHandler<FindDuplicateCandidatesQuery, IReadOnlyList<DuplicateCandidateDto>>
{
    public async Task<Result<IReadOnlyList<DuplicateCandidateDto>>> Handle(
        FindDuplicateCandidatesQuery query, CancellationToken ct) =>
        await db.FindDuplicates(query.Email, query.Phone, query.ExcludeId, ct);
}
