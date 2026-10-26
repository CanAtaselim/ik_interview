using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Common;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.Candidates;

public sealed record CandidateListItemDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? City,
    string? SourceName,
    IReadOnlyList<string> Tags,
    bool InTalentPool,
    int DocumentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    CandidateCvDto? Cv,
    BlacklistStatus? Blacklist,
    IReadOnlyList<CandidateProcessDto> Processes);

/// <summary>Listeden açılacak CV: adayın en son yüklenen CV türündeki dosyası.</summary>
public sealed record CandidateCvDto(Guid DocumentId, string FileName, string ContentType);

/// <summary>
/// Adayın bir pozisyondaki süreci. Listede açık süreçler gösterilir; hiç açık süreç yoksa
/// en son sonlanan tek süreç gelir.
/// </summary>
public sealed record CandidateProcessDto(
    Guid ApplicationId,
    Guid PositionId,
    string PositionTitle,
    string StageName,
    StageKind StageKind,
    ApplicationStatus Status);

public sealed record DuplicateCandidateDto(Guid Id, string FullName, string? Email, string? Phone);

/// <param name="TypeId">Sözlükteki dosya türü; <c>null</c> türü belirtilmemiş (eski) dosyadır ve CV sayılır.</param>
public sealed record CandidateDocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid? TypeId,
    string TypeName,
    string? UploadedByName,
    DateTimeOffset UploadedAt);

public sealed record MoneyDto(decimal Amount, string? Currency, bool IsNet);

public sealed record EducationDto(string? School, string? Department, string? Degree, int? GraduationYear);

public sealed record LanguageSkillDto(string? Language, LanguageLevel? Level);

/// <summary>
/// Ön görüşme kaydının İK görünümü: ücret bilgisi ve İK yorumu dahildir. Takım lideri görünümü
/// (CV önerme modülü) bu alanları içermeyen ayrı bir DTO kullanacak.
/// </summary>
public sealed record ScreeningDto(
    Guid Id,
    string? InterviewerName,
    ScreeningChannel? Channel,
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
    MoneyDto? ExpectedSalary,
    decimal? ExpectedSalaryMax,
    int? NoticePeriodWeeks,
    string? HrComment,
    MoneyDto? CurrentSalary,
    WorkModel? WorkModel,
    string? LocationProvince,
    string? LocationDistrict,
    string? LocationNote,
    bool? OpenToRelocation,
    bool? HasTravelRestriction,
    MilitaryStatus? MilitaryStatus,
    int? OverallRating,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CandidateDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? LinkedInUrl,
    string? GitHubUrl,
    string? City,
    Guid? SourceId,
    string? SourceName,
    IReadOnlyList<string> Tags,
    string? Notes,
    bool InTalentPool,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CandidateDocumentDto> Documents,
    IReadOnlyList<ScreeningDto> Screenings,
    IReadOnlyList<DuplicateCandidateDto> PossibleDuplicates,
    CandidateBlacklistDto? Blacklist);

/// <summary>
/// Adayın açık (onay bekleyen veya yürürlükteki) kara liste kaydı. Neden ve gerekçe burada yoktur;
/// ayrıntı, görüntülemeyi kayda geçiren kara liste ucundan alınır.
/// </summary>
public sealed record CandidateBlacklistDto(Guid EntryId, BlacklistStatus Status);

public sealed record TimelineEntryDto(
    Guid Id, TimelineEntryType Type, string Summary, string? ActorName, DateTimeOffset OccurredAt);

internal static class CandidateMappings
{
    /// <summary>Varsayılan CV türünün sözlükteki adı; türü olmayan eski dosyalar da bu adla anılır.</summary>
    public const string CvTypeName = "CV";

    public static CandidateDocumentDto ToDto(
        this CandidateDocument document,
        IReadOnlyDictionary<Guid, string> userNames,
        IReadOnlyDictionary<Guid, string> typeNames) =>
        new(
            document.Id,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.TypeId,
            document.TypeId is { } typeId ? typeNames.GetValueOrDefault(typeId, "Diğer") : CvTypeName,
            userNames.GetValueOrDefault(document.UploadedByUserId),
            document.UploadedAt);

    public static MoneyDto? ToDto(this Money? money) =>
        money is null ? null : new MoneyDto(money.Amount, money.Currency, money.IsNet);

    public static Money? ToMoney(this MoneyDto? dto) =>
        dto is null ? null : new Money(dto.Amount, dto.Currency, dto.IsNet);

    public static ScreeningDto ToDto(this Screening screening, IReadOnlyDictionary<Guid, string> userNames) =>
        new(
            screening.Id,
            userNames.GetValueOrDefault(screening.InterviewerUserId),
            screening.Channel,
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
            screening.ExpectedSalary.ToDto(),
            screening.ExpectedSalaryMax,
            screening.NoticePeriodWeeks,
            screening.HrComment,
            screening.CurrentSalary.ToDto(),
            screening.WorkModel,
            screening.LocationProvince,
            screening.LocationDistrict,
            screening.LocationNote,
            screening.OpenToRelocation,
            screening.HasTravelRestriction,
            screening.MilitaryStatus,
            screening.OverallRating,
            screening.CreatedAt,
            screening.UpdatedAt);
}
