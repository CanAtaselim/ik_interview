using FluentValidation;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Application.Features.Candidates;

/// <summary>Aday ekleme ve düzenleme command'larının ortak alanları.</summary>
public interface ICandidateFields
{
    string FirstName { get; }
    string LastName { get; }
    string? Email { get; }
    string? Phone { get; }
    string? LinkedInUrl { get; }
    string? GitHubUrl { get; }
    string? City { get; }
    Guid? SourceId { get; }
    IReadOnlyCollection<string>? Tags { get; }
    string? Notes { get; }

    /// <summary>İK mükerrer uyarısını görüp yine de kaydetmek istediğini bildirdi.</summary>
    bool ConfirmDuplicate { get; }

    /// <summary>İK kara liste eşleşmesi uyarısını görüp yine de kaydetmek istediğini bildirdi.</summary>
    bool ConfirmBlacklist { get; }
}

internal abstract class CandidateFieldsValidator<T> : AbstractValidator<T>
    where T : ICandidateFields
{
    private const int MaxTags = 20;
    private const int MinPhoneDigits = 7;

    protected CandidateFieldsValidator()
    {
        RuleFor(c => c.FirstName)
            .NotEmpty().WithMessage("Adayın adını yazmalısınız.")
            .MaximumLength(Candidate.NameMaxLength).WithMessage($"Ad en fazla {Candidate.NameMaxLength} karakter olabilir.");
        RuleFor(c => c.LastName)
            .NotEmpty().WithMessage("Adayın soyadını yazmalısınız.")
            .MaximumLength(Candidate.NameMaxLength).WithMessage($"Soyad en fazla {Candidate.NameMaxLength} karakter olabilir.");

        RuleFor(c => c.Email)
            .Must((candidate, email) => !string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(candidate.Phone))
            .WithMessage("E-posta veya telefon alanlarından en az biri dolu olmalı.");
        RuleFor(c => c.Email)
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi yazın.")
            .MaximumLength(320).WithMessage("E-posta en fazla 320 karakter olabilir.")
            .When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.Phone)
            .Must(phone => phone!.Count(char.IsAsciiDigit) >= MinPhoneDigits)
            .WithMessage("Geçerli bir telefon numarası yazın.")
            .MaximumLength(30).WithMessage("Telefon en fazla 30 karakter olabilir.")
            .When(c => !string.IsNullOrWhiteSpace(c.Phone));

        RuleFor(c => c.LinkedInUrl).WebAddress("LinkedIn");
        RuleFor(c => c.GitHubUrl).WebAddress("GitHub");
        RuleFor(c => c.City).MaximumLength(100).WithMessage("Şehir en fazla 100 karakter olabilir.");
        RuleFor(c => c.Notes).MaximumLength(4000).WithMessage("Notlar en fazla 4000 karakter olabilir.");
        RuleFor(c => c.Tags)
            .Must(tags => tags is null || tags.Count <= MaxTags)
            .WithMessage($"En fazla {MaxTags} etiket ekleyebilirsiniz.");
        RuleForEach(c => c.Tags)
            .MaximumLength(50).WithMessage("Bir etiket en fazla 50 karakter olabilir.")
            .OverridePropertyName(nameof(ICandidateFields.Tags));
    }
}

internal static class CandidateFieldsExtensions
{
    public static IRuleBuilderOptions<T, string?> WebAddress<T>(this IRuleBuilder<T, string?> rule, string siteName) =>
        rule.Must(url => string.IsNullOrWhiteSpace(url) ||
                         (url.Length <= 300 &&
                          Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
                          (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)))
            .WithMessage($"{siteName} adresi \"https://\" ile başlayan geçerli bir bağlantı olmalı.");

    public static CandidateProfile ToProfile(this ICandidateFields fields) =>
        new(
            fields.FirstName,
            fields.LastName,
            fields.Email,
            fields.Phone,
            fields.LinkedInUrl,
            fields.GitHubUrl,
            fields.City,
            fields.SourceId,
            fields.Tags ?? [],
            fields.Notes);

    /// <summary>Seçilen kaynak geçerli değilse alan hatası döner; geçerliyse (veya boşsa) <c>null</c>.</summary>
    public static async Task<Error?> FindInvalidSource(
        this IAppDbContext db, ICandidateFields fields, CancellationToken ct)
    {
        if (fields.SourceId is not { } sourceId || await db.IsSelectable(sourceId, DictionaryType.CandidateSource, ct))
            return null;

        return Error.Validation(new Dictionary<string, string[]> { ["sourceId"] = ["Geçerli bir kaynak seçmelisiniz."] });
    }
}
