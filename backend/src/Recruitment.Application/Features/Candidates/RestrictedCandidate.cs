using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Candidates;

namespace Recruitment.Application.Features.Candidates;

/// <summary>
/// Ön görüşme kaydının İK dışı görünümü (CV'nin önerildiği takım lideri, mülakatçı). Ücret
/// beklentisi, mevcut ücret, İK yorumu ve İK puanı bu kayıtta hiç yer
/// almaz (arayüzde gizlemek yetmez; veri hiç gönderilmez).
/// </summary>
public sealed record RestrictedScreeningDto(
    Guid Id,
    string? InterviewerName,
    DateTimeOffset? InterviewedAt,
    IReadOnlyList<EducationDto> Educations,
    string? EducationNote,
    IReadOnlyList<LanguageSkillDto> Languages,
    string? LanguagesNote,
    string? SoftSkills,
    IReadOnlyList<string> TechnicalSkillTags,
    string? TechnicalSkills,
    string? ExperienceSummary,
    string? Motivation,
    int? TotalExperienceMonths,
    int? FieldExperienceMonths,
    int? NoticePeriodWeeks,
    WorkModel? WorkModel,
    string? LocationProvince,
    string? LocationDistrict,
    string? LocationNote,
    bool? OpenToRelocation,
    bool? HasTravelRestriction,
    MilitaryStatus? MilitaryStatus);

/// <summary>
/// Adayın İK dışı görünümü: iletişim bilgisi, İK notu, ücret ve kara liste bilgisi içermez.
/// </summary>
public sealed record RestrictedCandidateDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? City,
    string? LinkedInUrl,
    string? GitHubUrl,
    IReadOnlyList<string> Tags,
    IReadOnlyList<CandidateDocumentDto> Documents,
    IReadOnlyList<RestrictedScreeningDto> Screenings);

internal static class RestrictedCandidateQueries
{
    /// <summary>Adayın kısıtlı görünümünü yükler; yetki kontrolü çağıranın sorumluluğundadır.</summary>
    public static async Task<RestrictedCandidateDto?> LoadRestrictedCandidate(
        this IAppDbContext db, Guid candidateId, CancellationToken ct)
    {
        var candidate = await db.Candidates.AsNoTracking()
            .Include(c => c.Documents)
            .Include(c => c.Screenings)
            .FirstOrDefaultAsync(c => c.Id == candidateId, ct);
        if (candidate is null)
            return null;

        var userIds = candidate.Documents.Select(d => d.UploadedByUserId)
            .Concat(candidate.Screenings.Select(s => s.InterviewerUserId))
            .Distinct()
            .ToList();
        var userNames = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var typeNames = await db.ItemNames(candidate.Documents.Select(d => d.TypeId).OfType<Guid>().Distinct(), ct);

        return new RestrictedCandidateDto(
            candidate.Id,
            candidate.FirstName,
            candidate.LastName,
            candidate.City,
            candidate.LinkedInUrl,
            candidate.GitHubUrl,
            candidate.Tags,
            [.. candidate.Documents
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => d.ToDto(userNames, typeNames))],
            [.. candidate.Screenings.OrderByDescending(s => s.CreatedAt).Select(s => s.ToRestrictedDto(userNames))]);
    }

    /// <summary>Adayın dosyası için erişimi kayda geçirip kısa süreli indirme bağlantısı üretir.</summary>
    public static async Task<Common.Result<CreateDocumentDownloadLink.DownloadLinkDto>> CreateRestrictedDocumentLink(
        this IAppDbContext db,
        IDownloadTokenService tokens,
        Guid userId,
        Guid candidateId,
        Guid documentId,
        TimeProvider clock,
        CancellationToken ct)
    {
        var exists = await db.Candidates
            .Where(c => c.Id == candidateId)
            .SelectMany(c => c.Documents)
            .AnyAsync(d => d.Id == documentId, ct);
        if (!exists)
            return CandidateErrors.DocumentNotFound;

        db.AccessLogs.Add(Domain.Audit.AccessLog.CvDownloaded(userId, candidateId, documentId, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = tokens.Create(documentId);
        return new CreateDocumentDownloadLink.DownloadLinkDto(token, expiresAt);
    }

    private static RestrictedScreeningDto ToRestrictedDto(this Screening screening, IReadOnlyDictionary<Guid, string> userNames) =>
        new(
            screening.Id,
            userNames.GetValueOrDefault(screening.InterviewerUserId),
            screening.InterviewedAt,
            [.. screening.Educations.Select(e => new EducationDto(e.School, e.Department, e.Degree, e.GraduationYear))],
            screening.EducationNote,
            [.. screening.Languages.Select(l => new LanguageSkillDto(l.Language, l.Level))],
            screening.LanguagesNote,
            screening.SoftSkills,
            screening.TechnicalSkillTags,
            screening.TechnicalSkills,
            screening.ExperienceSummary,
            screening.Motivation,
            screening.TotalExperienceMonths,
            screening.FieldExperienceMonths,
            screening.NoticePeriodWeeks,
            screening.WorkModel,
            screening.LocationProvince,
            screening.LocationDistrict,
            screening.LocationNote,
            screening.OpenToRelocation,
            screening.HasTravelRestriction,
            screening.MilitaryStatus);
}
