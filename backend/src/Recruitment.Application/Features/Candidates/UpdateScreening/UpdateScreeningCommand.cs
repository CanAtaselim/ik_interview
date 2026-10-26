using FluentValidation;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Candidates;

namespace Recruitment.Application.Features.Candidates.UpdateScreening;

/// <summary>Ön görüşme formunu kaydeder (form yazılırken otomatik çağrılır). Hiçbir alan zorunlu değildir.</summary>
public sealed record UpdateScreeningCommand(
    Guid CandidateId,
    Guid ScreeningId,
    ScreeningChannel? Channel,
    DateTimeOffset? InterviewedAt,
    IReadOnlyList<EducationDto>? Educations,
    string? EducationNote,
    IReadOnlyList<LanguageSkillDto>? Languages,
    string? LanguagesNote,
    string? SoftSkills,
    IReadOnlyCollection<string>? TechnicalSkillTags,
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
    int? OverallRating) : ICommand<Unit>;

// Yalnızca biçim doğrulanır; "bu alan dolu olmalı" türünden hiçbir kural yoktur.
internal sealed class UpdateScreeningCommandValidator : AbstractValidator<UpdateScreeningCommand>
{
    private const int MaxTextLength = 4000;
    private const int MaxListItems = 20;

    public UpdateScreeningCommandValidator()
    {
        RuleFor(c => c.OverallRating).InclusiveBetween(1, 5).WithMessage("Genel puan 1 ile 5 arasında olmalı.");
        RuleFor(c => c.TotalExperienceMonths)
            .InclusiveBetween(0, 70 * 12).WithMessage("Toplam deneyim 0 ile 70 yıl arasında olmalı.");
        RuleFor(c => c.FieldExperienceMonths)
            .InclusiveBetween(0, 70 * 12).WithMessage("Alan deneyimi 0 ile 70 yıl arasında olmalı.");
        RuleFor(c => c.NoticePeriodWeeks)
            .InclusiveBetween(0, 104).WithMessage("İhbar süresi 0 ile 104 hafta arasında olmalı.");

        RuleFor(c => c.ExpectedSalary!.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("Ücret beklentisi negatif olamaz.")
            .OverridePropertyName(nameof(UpdateScreeningCommand.ExpectedSalary))
            .When(c => c.ExpectedSalary is not null);
        RuleFor(c => c.ExpectedSalaryMax)
            .GreaterThanOrEqualTo(0).WithMessage("Ücret beklentisi negatif olamaz.");
        RuleFor(c => c)
            .Must(c => c.ExpectedSalaryMax is null || c.ExpectedSalary is not null)
            .WithMessage("Ücret aralığının üst değerini girmeden önce alt değerini girin.")
            .OverridePropertyName(nameof(UpdateScreeningCommand.ExpectedSalaryMax));
        RuleFor(c => c)
            .Must(c => c.ExpectedSalaryMax is null || c.ExpectedSalary is null || c.ExpectedSalaryMax >= c.ExpectedSalary.Amount)
            .WithMessage("Ücret beklentisinin üst değeri alt değerinden küçük olamaz.")
            .OverridePropertyName(nameof(UpdateScreeningCommand.ExpectedSalaryMax));
        RuleFor(c => c.CurrentSalary!.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("Mevcut ücret negatif olamaz.")
            .OverridePropertyName(nameof(UpdateScreeningCommand.CurrentSalary))
            .When(c => c.CurrentSalary is not null);

        foreach (var (text, label) in new (Func<UpdateScreeningCommand, string?> Text, string Label)[]
                 {
                     (c => c.SoftSkills, "Beşeri yetkinlik"), (c => c.TechnicalSkills, "Teknik yetkinlik"),
                     (c => c.ExperienceSummary, "Deneyim özeti"), (c => c.Motivation, "Motivasyon"),
                     (c => c.HrComment, "İK yorumu"), (c => c.EducationNote, "Eğitim açıklaması"),
                     (c => c.LanguagesNote, "Yabancı dil açıklaması"), (c => c.LocationNote, "Lokasyon açıklaması"),
                 })
        {
            RuleFor(c => c)
                .Must(c => (text(c)?.Length ?? 0) <= MaxTextLength)
                .WithMessage($"{label} en fazla {MaxTextLength} karakter olabilir.")
                .OverridePropertyName("form");
        }

        RuleFor(c => c.LocationProvince).MaximumLength(100).WithMessage("İl en fazla 100 karakter olabilir.");
        RuleFor(c => c.LocationDistrict).MaximumLength(100).WithMessage("İlçe en fazla 100 karakter olabilir.");
        RuleFor(c => c)
            .Must(c => string.IsNullOrWhiteSpace(c.LocationDistrict) || !string.IsNullOrWhiteSpace(c.LocationProvince))
            .WithMessage("İlçeyi seçmeden önce ili seçin.")
            .OverridePropertyName(nameof(UpdateScreeningCommand.LocationDistrict));
        RuleFor(c => c.Educations)
            .Must(list => list is null || list.Count <= MaxListItems)
            .WithMessage($"En fazla {MaxListItems} eğitim satırı ekleyebilirsiniz.");
        RuleForEach(c => c.Educations)
            .Must(e => e.GraduationYear is null or (>= 1950 and <= 2100))
            .WithMessage("Mezuniyet yılı 1950 ile 2100 arasında olmalı.")
            .OverridePropertyName(nameof(UpdateScreeningCommand.Educations));
        RuleFor(c => c.Languages)
            .Must(list => list is null || list.Count <= MaxListItems)
            .WithMessage($"En fazla {MaxListItems} yabancı dil ekleyebilirsiniz.");
        RuleFor(c => c.TechnicalSkillTags)
            .Must(tags => tags is null || (tags.Count <= 30 && tags.All(t => t.Length <= 50)))
            .WithMessage("En fazla 30 teknik yetkinlik etiketi ekleyebilirsiniz (her biri en fazla 50 karakter).");
    }
}
