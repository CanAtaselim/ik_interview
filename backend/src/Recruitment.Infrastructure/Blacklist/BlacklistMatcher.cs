using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Candidates;
using Recruitment.Infrastructure.Persistence;

namespace Recruitment.Infrastructure.Blacklist;

/// <summary>
/// Kara liste eşleştirmesi. E-posta ve telefon birebir karşılaştırılır; ad soyad için PostgreSQL
/// pg_trgm benzerliği kullanılır ve bu yalnızca "olası eşleşme" sayılır.
/// </summary>
internal sealed class BlacklistMatcher(AppDbContext db, TimeProvider clock) : IBlacklistMatcher
{
    private const double NameSimilarityThreshold = 0.6;
    private const int MinPhoneDigitsForMatch = 7;
    private const int MaxMatches = 5;

    public async Task<IReadOnlyList<BlacklistMatch>> FindMatches(
        string? email, string? phone, string? firstName, string? lastName, Guid? excludeCandidateId, CancellationToken ct)
    {
        var normalizedEmail = ContactNormalizer.NormalizeEmail(email);
        var normalizedPhone = ContactNormalizer.NormalizePhone(phone);
        if (normalizedPhone is { Length: < MinPhoneDigitsForMatch })
            normalizedPhone = null;
        var normalizedName = ContactNormalizer.NormalizeName(firstName, lastName);

        if (normalizedEmail is null && normalizedPhone is null && normalizedName.Length == 0)
            return [];

        var now = clock.GetUtcNow();
        var matches = await db.BlacklistEntries.AsNoTracking()
            // Süresi dolmuş ama günlük iş henüz işlememiş kayıt da yürürlükte sayılmaz.
            .Where(e => e.Status == BlacklistStatus.Active && (e.ExpiresAt == null || e.ExpiresAt > now))
            .Where(e => e.CandidateId != excludeCandidateId)
            .Join(db.Candidates.AsNoTracking(), e => e.CandidateId, c => c.Id, (e, c) => new
            {
                EntryId = e.Id,
                CandidateId = c.Id,
                c.FirstName,
                c.LastName,
                e.ReasonId,
                IsExact =
                    (normalizedEmail != null && c.NormalizedEmail == normalizedEmail) ||
                    (normalizedPhone != null && c.NormalizedPhone == normalizedPhone),
                IsSimilar =
                    normalizedName != "" &&
                    EF.Functions.TrigramsSimilarity(c.NormalizedName, normalizedName) >= NameSimilarityThreshold,
            })
            .Where(m => m.IsExact || m.IsSimilar)
            .OrderByDescending(m => m.IsExact)
            .Take(MaxMatches)
            .ToListAsync(ct);

        var reasonIds = matches.Select(m => m.ReasonId).Distinct().ToList();
        var reasonNames = await db.DictionaryItems.AsNoTracking()
            .Where(d => reasonIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        return matches
            .Select(m => new BlacklistMatch(
                m.EntryId,
                m.CandidateId,
                $"{m.FirstName} {m.LastName}",
                reasonNames.GetValueOrDefault(m.ReasonId, string.Empty),
                m.IsExact ? BlacklistMatchKind.Exact : BlacklistMatchKind.Possible))
            .ToList();
    }
}
