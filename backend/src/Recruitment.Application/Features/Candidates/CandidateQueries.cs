using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Candidates;

namespace Recruitment.Application.Features.Candidates;

internal static class CandidateQueries
{
    // Yazılırken gelen kısa telefon parçaları ("053") herkesle eşleşmesin.
    private const int MinPhoneDigitsForMatch = 7;
    private const int MaxDuplicatesShown = 5;

    /// <summary>
    /// <see cref="Candidate.Tags"/> salt okunur liste olduğu için EF'te arkasındaki alan eşlenir;
    /// sorgularda etiketlere bu ifadeyle ulaşılır.
    /// </summary>
    public static readonly Expression<Func<Candidate, List<string>>> Tags =
        c => EF.Property<List<string>>(c, "_tags");

    /// <summary>Adayı dosyaları ve ön görüşme kayıtlarıyla birlikte, değiştirilmek üzere yükler.</summary>
    public static Task<Candidate?> FindCandidate(this IAppDbContext db, Guid id, CancellationToken ct) =>
        db.Candidates
            .Include(c => c.Documents)
            .Include(c => c.Screenings)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <summary>Aynı e-posta veya telefona (son 10 hane) sahip diğer adaylar.</summary>
    public static async Task<List<DuplicateCandidateDto>> FindDuplicates(
        this IAppDbContext db, string? email, string? phone, Guid? excludeId, CancellationToken ct)
    {
        var normalizedEmail = ContactNormalizer.NormalizeEmail(email);
        var normalizedPhone = ContactNormalizer.NormalizePhone(phone);
        if (normalizedPhone is { Length: < MinPhoneDigitsForMatch })
            normalizedPhone = null;

        if (normalizedEmail is null && normalizedPhone is null)
            return [];

        return await db.Candidates.AsNoTracking()
            .Where(c => c.Id != excludeId)
            .Where(c =>
                (normalizedEmail != null && c.NormalizedEmail == normalizedEmail) ||
                (normalizedPhone != null && c.NormalizedPhone == normalizedPhone))
            .OrderBy(c => c.CreatedAt)
            .Take(MaxDuplicatesShown)
            .Select(c => new DuplicateCandidateDto(c.Id, c.FirstName + " " + c.LastName, c.Email, c.Phone))
            .ToListAsync(ct);
    }
}
