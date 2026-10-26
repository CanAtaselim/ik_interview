using Recruitment.Domain.Common;

namespace Recruitment.Domain.Candidates;

/// <summary>
/// İK ön görüşme kaydı. Bir adayın birden çok kaydı olabilir; form istenildiği zaman, istenildiği
/// kadar doldurulur. Yalnızca biçim kuralları vardır (puan 1–5, süreler negatif olamaz).
/// </summary>
public sealed class Screening : Entity
{
    private const decimal MaxExperienceYears = 70;
    private const int MaxExperienceMonths = 70 * 12;

    private readonly List<Education> _educations = [];
    private readonly List<LanguageSkill> _languages = [];
    private readonly List<string> _technicalSkillTags = [];

    private Screening() { }

    internal Screening(Guid interviewerUserId) => InterviewerUserId = interviewerUserId;

    public Guid InterviewerUserId { get; private set; }
    public ScreeningChannel? Channel { get; private set; }
    public DateTimeOffset? InterviewedAt { get; private set; }
    public string? SoftSkills { get; private set; }
    public string? TechnicalSkills { get; private set; }
    public string? ExperienceSummary { get; private set; }
    public string? Motivation { get; private set; }
    public string? EducationNote { get; private set; }
    public string? LanguagesNote { get; private set; }

    /// <summary>Toplam iş deneyimi ay olarak tutulur (ör. 3 yıl 4 ay = 40); arayüz yıl ve ay gösterir.</summary>
    public int? TotalExperienceMonths { get; private set; }

    /// <summary>Alan deneyimi de ay olarak tutulur (toplam iş deneyimiyle aynı biçim).</summary>
    public int? FieldExperienceMonths { get; private set; }

    /// <summary>Ücret beklentisinin alt değeri (veya tek değeri); para birimi ve net/brüt bilgisini taşır.</summary>
    public Money? ExpectedSalary { get; private set; }

    /// <summary>Ücret beklentisi aralığının üst değeri; aralık girilmediyse <c>null</c>.</summary>
    public decimal? ExpectedSalaryMax { get; private set; }

    public int? NoticePeriodWeeks { get; private set; }
    public string? HrComment { get; private set; }
    public Money? CurrentSalary { get; private set; }
    public WorkModel? WorkModel { get; private set; }
    public string? LocationProvince { get; private set; }
    public string? LocationDistrict { get; private set; }
    public string? LocationNote { get; private set; }
    public bool? OpenToRelocation { get; private set; }

    /// <summary>Seyahat engeli var mı (<c>true</c>), yok mu (<c>false</c>), sorulmadı mı (<c>null</c>)?</summary>
    public bool? HasTravelRestriction { get; private set; }
    public MilitaryStatus? MilitaryStatus { get; private set; }
    public int? OverallRating { get; private set; }

    public IReadOnlyList<Education> Educations => _educations;
    public IReadOnlyList<LanguageSkill> Languages => _languages;
    public IReadOnlyList<string> TechnicalSkillTags => _technicalSkillTags;

    internal void Update(ScreeningDetails details)
    {
        if (details.OverallRating is < 1 or > 5)
            throw new DomainException("Genel puan 1 ile 5 arasında olmalı.");
        if (details.TotalExperienceMonths is < 0 or > MaxExperienceMonths ||
            details.FieldExperienceMonths is < 0 or > MaxExperienceMonths)
            throw new DomainException($"Deneyim süresi 0 ile {MaxExperienceYears} yıl arasında olmalı.");
        if (details.ExpectedSalaryMax is { } max)
        {
            if (details.ExpectedSalary is null)
                throw new DomainException("Ücret aralığının üst değerini girmeden önce alt değerini girin.");
            if (max < details.ExpectedSalary.Amount)
                throw new DomainException("Ücret beklentisinin üst değeri alt değerinden küçük olamaz.");
        }
        if (Clean(details.LocationDistrict) is not null && Clean(details.LocationProvince) is null)
            throw new DomainException("İlçeyi seçmeden önce ili seçin.");
        if (details.NoticePeriodWeeks is < 0)
            throw new DomainException("İhbar süresi negatif olamaz.");

        Channel = details.Channel;
        InterviewedAt = details.InterviewedAt;
        SoftSkills = Clean(details.SoftSkills);
        TechnicalSkills = Clean(details.TechnicalSkills);
        ExperienceSummary = Clean(details.ExperienceSummary);
        Motivation = Clean(details.Motivation);
        EducationNote = Clean(details.EducationNote);
        LanguagesNote = Clean(details.LanguagesNote);
        TotalExperienceMonths = details.TotalExperienceMonths;
        FieldExperienceMonths = details.FieldExperienceMonths;
        ExpectedSalary = details.ExpectedSalary;
        ExpectedSalaryMax = details.ExpectedSalaryMax;
        NoticePeriodWeeks = details.NoticePeriodWeeks;
        HrComment = Clean(details.HrComment);
        CurrentSalary = details.CurrentSalary;
        WorkModel = details.WorkModel;
        LocationProvince = Clean(details.LocationProvince);
        LocationDistrict = LocationProvince is null ? null : Clean(details.LocationDistrict);
        LocationNote = Clean(details.LocationNote);
        OpenToRelocation = details.OpenToRelocation;
        HasTravelRestriction = details.HasTravelRestriction;
        MilitaryStatus = details.MilitaryStatus;
        OverallRating = details.OverallRating;

        _educations.Clear();
        _educations.AddRange(details.Educations.Where(e =>
            Clean(e.School) is not null || Clean(e.Department) is not null ||
            Clean(e.Degree) is not null || e.GraduationYear is not null));

        _languages.Clear();
        _languages.AddRange(details.Languages.Where(l => Clean(l.Language) is not null));

        _technicalSkillTags.Clear();
        _technicalSkillTags.AddRange(details.TechnicalSkillTags
            .Select(Clean)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Aramaya girecek metinler. Ücret bilgisi ve İK yorumu bilerek dışarıda bırakılır.</summary>
    internal IEnumerable<string?> SearchableTexts() =>
    [
        SoftSkills, TechnicalSkills, ExperienceSummary, Motivation,
        LocationProvince, LocationDistrict, LocationNote, EducationNote, LanguagesNote,
        .. _technicalSkillTags,
        .. _educations.SelectMany(e => new[] { e.School, e.Department, e.Degree }),
        .. _languages.Select(l => l.Language),
    ];

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
