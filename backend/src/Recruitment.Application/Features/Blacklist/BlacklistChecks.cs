using Recruitment.Application.Abstractions;
using Recruitment.Domain.Candidates;

namespace Recruitment.Application.Features.Blacklist;

internal static class BlacklistChecks
{
    /// <summary>
    /// Kayıtlı bir adayla ilgili işlemlerde (CV önerme, mülakat planlama) sorulan kara liste
    /// eşleşmeleri: adayın kendi kaydı ve aynı e-posta/telefon. Ad benzerliği aday eklenirken sorulmuştur.
    /// </summary>
    public static async Task<List<BlacklistMatch>> FindForCandidate(
        this IBlacklistMatcher matcher, Candidate candidate, CancellationToken ct) =>
        [.. (await matcher.FindMatches(
                candidate.Email, candidate.Phone, candidate.FirstName, candidate.LastName, excludeCandidateId: null, ct))
            .Where(m => m.Kind == BlacklistMatchKind.Exact || m.CandidateId == candidate.Id)];
}
