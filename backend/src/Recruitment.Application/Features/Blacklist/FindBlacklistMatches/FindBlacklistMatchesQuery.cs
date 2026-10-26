using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Blacklist.FindBlacklistMatches;

/// <summary>Form doldurulurken, yazılan kişi kara listedeki biriyle eşleşiyor mu diye bakar.</summary>
public sealed record FindBlacklistMatchesQuery(
    string? Email, string? Phone, string? FirstName, string? LastName, Guid? ExcludeCandidateId)
    : IQuery<IReadOnlyList<BlacklistMatch>>;

internal sealed class FindBlacklistMatchesQueryHandler(IBlacklistMatcher matcher)
    : IQueryHandler<FindBlacklistMatchesQuery, IReadOnlyList<BlacklistMatch>>
{
    public async Task<Result<IReadOnlyList<BlacklistMatch>>> Handle(FindBlacklistMatchesQuery query, CancellationToken ct) =>
        Result.From(await matcher.FindMatches(
            query.Email, query.Phone, query.FirstName, query.LastName, query.ExcludeCandidateId, ct));
}
