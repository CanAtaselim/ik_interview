using Recruitment.Domain.Common;

namespace Recruitment.Domain.Candidates;

public sealed class Education
{
    private Education() { }

    public Education(string? school, string? department, string? degree, int? graduationYear)
    {
        School = school;
        Department = department;
        Degree = degree;
        GraduationYear = graduationYear;
    }

    public string? School { get; private set; }
    public string? Department { get; private set; }
    public string? Degree { get; private set; }
    public int? GraduationYear { get; private set; }
}

public sealed class LanguageSkill
{
    private LanguageSkill() { }

    public LanguageSkill(string? language, LanguageLevel? level)
    {
        Language = language;
        Level = level;
    }

    public string? Language { get; private set; }
    public LanguageLevel? Level { get; private set; }
}

/// <summary>İK ön görüşme formunun içeriği. Hiçbir alan zorunlu değildir.</summary>
public sealed record ScreeningDetails(
    ScreeningChannel? Channel,
    DateTimeOffset? InterviewedAt,
    IReadOnlyList<Education> Educations,
    string? EducationNote,
    IReadOnlyList<LanguageSkill> Languages,
    string? LanguagesNote,
    string? SoftSkills,
    IReadOnlyCollection<string> TechnicalSkillTags,
    string? TechnicalSkills,
    string? ExperienceSummary,
    string? Motivation,
    int? TotalExperienceMonths,
    int? FieldExperienceMonths,
    Money? ExpectedSalary,
    decimal? ExpectedSalaryMax,
    int? NoticePeriodWeeks,
    string? HrComment,
    Money? CurrentSalary,
    WorkModel? WorkModel,
    string? LocationProvince,
    string? LocationDistrict,
    string? LocationNote,
    bool? OpenToRelocation,
    bool? HasTravelRestriction,
    MilitaryStatus? MilitaryStatus,
    int? OverallRating);
