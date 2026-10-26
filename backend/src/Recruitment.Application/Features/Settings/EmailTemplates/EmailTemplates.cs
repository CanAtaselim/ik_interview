using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Notifications;
using Recruitment.Domain.Notifications;

namespace Recruitment.Application.Features.Settings.EmailTemplates;

public sealed record EmailTemplateDto(
    Guid Id, string Code, string Name, string Subject, string Body, string Variables, bool IsModified);

public sealed record EmailPreviewDto(string Subject, string Html);

/// <summary>E-posta şablonlarını listeler; <c>IsModified</c> varsayılan metinden farklı olanları işaretler.</summary>
public sealed record GetEmailTemplatesQuery : IQuery<IReadOnlyList<EmailTemplateDto>>;

internal static partial class EmailTemplateRules
{
    /// <summary>Şablonda geçen <c>{Degisken}</c> adları.</summary>
    public static IEnumerable<string> VariablesIn(string text) =>
        VariablePattern().Matches(text).Select(m => m.Groups[1].Value).Distinct();

    /// <summary>Tanımlı değişkenlerin dışında kalan alanlar (yazım hatası, boş kalacak alan).</summary>
    public static List<string> Unknown(string allowedVariables, params string[] texts)
    {
        var allowed = VariablesIn(allowedVariables).ToHashSet();
        return [.. texts.SelectMany(VariablesIn).Where(v => !allowed.Contains(v)).Distinct()];
    }

    public static Dictionary<string, string?> SampleValues() => new()
    {
        ["AdSoyad"] = "Ayşe Yılmaz",
        ["Baslik"] = "Size bir CV önerildi: Deniz Aday",
        ["Mesaj"] = "Mehmet Demir önerdi · Backend Geliştirici · Son tarih: 22.10.2026",
        ["Baglanti"] = "http://localhost:8080/cv-reviews/ornek",
        ["AdayAdi"] = "Deniz Aday",
        ["Pozisyon"] = "Backend Geliştirici",
        ["MulakatTarihi"] = "21 Ekim 2026 Çarşamba 14:00–15:00",
        ["Sure"] = "60 dakika",
        ["Yer"] = "Toplantı Odası 2",
        ["MulakatTipi"] = "Teknik",
    };

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex VariablePattern();
}

internal sealed class GetEmailTemplatesQueryHandler(IAppDbContext db)
    : IQueryHandler<GetEmailTemplatesQuery, IReadOnlyList<EmailTemplateDto>>
{
    public async Task<Result<IReadOnlyList<EmailTemplateDto>>> Handle(GetEmailTemplatesQuery query, CancellationToken ct) =>
        (await db.EmailTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct))
            .Select(t => t.ToDto())
            .ToList();
}

internal static class EmailTemplateMappings
{
    public static EmailTemplateDto ToDto(this EmailTemplate template)
    {
        var defaults = DefaultEmailTemplates.Find(template.Code);
        var modified = defaults is not null &&
                       (template.Subject != defaults.Subject || template.Body.Trim() != defaults.Body.Trim());
        return new EmailTemplateDto(template.Id, template.Code, template.Name, template.Subject, template.Body, template.Variables, modified);
    }
}

/// <summary>E-posta şablonunun konusunu ve içeriğini günceller. Yalnızca şablondaki tanımlı alanlar kullanılabilir.</summary>
public sealed record UpdateEmailTemplateCommand(Guid Id, string Subject, string Body) : ICommand<Unit>;

internal sealed class UpdateEmailTemplateCommandValidator : AbstractValidator<UpdateEmailTemplateCommand>
{
    public UpdateEmailTemplateCommandValidator()
    {
        RuleFor(c => c.Subject)
            .NotEmpty().WithMessage("E-posta konusunu yazmalısınız.")
            .MaximumLength(EmailTemplate.SubjectMaxLength)
            .WithMessage($"Konu en fazla {EmailTemplate.SubjectMaxLength} karakter olabilir.");
        RuleFor(c => c.Body)
            .NotEmpty().WithMessage("E-posta içeriğini yazmalısınız.")
            .MaximumLength(20_000).WithMessage("İçerik en fazla 20.000 karakter olabilir.");
    }
}

internal sealed class UpdateEmailTemplateCommandHandler(IAppDbContext db)
    : ICommandHandler<UpdateEmailTemplateCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateEmailTemplateCommand command, CancellationToken ct)
    {
        var template = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Id == command.Id, ct);
        if (template is null)
            return SettingsErrors.EmailTemplateNotFound;

        if (EmailTemplateRules.Unknown(template.Variables, command.Subject) is { Count: > 0 } subjectUnknown)
            return SettingsErrors.Field("subject", UnknownMessage(subjectUnknown, template.Variables));
        if (EmailTemplateRules.Unknown(template.Variables, command.Body) is { Count: > 0 } bodyUnknown)
            return SettingsErrors.Field("body", UnknownMessage(bodyUnknown, template.Variables));

        template.Update(command.Subject, command.Body);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private static string UnknownMessage(IEnumerable<string> unknown, string allowed) =>
        $"Tanımsız alan: {string.Join(", ", unknown.Select(v => $"{{{v}}}"))}. Kullanabileceğiniz alanlar: {allowed}";
}

/// <summary>Şablonu varsayılan metnine döndürür.</summary>
public sealed record ResetEmailTemplateCommand(Guid Id) : ICommand<Unit>;

internal sealed class ResetEmailTemplateCommandHandler(IAppDbContext db) : ICommandHandler<ResetEmailTemplateCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ResetEmailTemplateCommand command, CancellationToken ct)
    {
        var template = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Id == command.Id, ct);
        if (template is null || DefaultEmailTemplates.Find(template.Code) is not { } defaults)
            return SettingsErrors.EmailTemplateNotFound;

        template.Update(defaults.Subject, defaults.Body);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Düzenlenen (henüz kaydedilmemiş olabilir) metnin örnek verilerle nasıl görüneceği.</summary>
public sealed record PreviewEmailTemplateQuery(Guid Id, string Subject, string Body) : IQuery<EmailPreviewDto>;

internal sealed class PreviewEmailTemplateQueryHandler(IAppDbContext db, EmailComposer composer)
    : IQueryHandler<PreviewEmailTemplateQuery, EmailPreviewDto>
{
    public async Task<Result<EmailPreviewDto>> Handle(PreviewEmailTemplateQuery query, CancellationToken ct)
    {
        if (!await db.EmailTemplates.AnyAsync(t => t.Id == query.Id, ct))
            return SettingsErrors.EmailTemplateNotFound;

        var (subject, html) = composer.Preview(query.Subject, query.Body, EmailTemplateRules.SampleValues());
        return new EmailPreviewDto(subject, html);
    }
}
