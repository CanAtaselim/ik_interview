using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.HiringRequests;

namespace Recruitment.Application.Features.HiringRequests;

/// <summary>Talep oluşturma ve düzenleme command'larının ortak alanları.</summary>
public interface IHiringRequestFields
{
    string Title { get; }
    Guid DepartmentId { get; }
    Guid LevelId { get; }
    int Headcount { get; }
    HiringReason Reason { get; }
    string? ReplacedEmployeeName { get; }
    Urgency Urgency { get; }
    DateOnly? ExpectedStartDate { get; }
    IReadOnlyCollection<string>? Competencies { get; }
    string? Description { get; }
}

internal abstract class HiringRequestFieldsValidator<T> : AbstractValidator<T>
    where T : IHiringRequestFields
{
    private const int MaxCompetencies = 20;

    protected HiringRequestFieldsValidator()
    {
        RuleFor(c => c.Title)
            .NotEmpty().WithMessage("Pozisyon adını seçmelisiniz.")
            .MaximumLength(HiringRequest.TitleMaxLength)
            .WithMessage($"Pozisyon adı en fazla {HiringRequest.TitleMaxLength} karakter olabilir.");
        RuleFor(c => c.DepartmentId).NotEmpty().WithMessage("Birim seçmelisiniz.");
        RuleFor(c => c.LevelId).NotEmpty().WithMessage("Seviye seçmelisiniz.");
        RuleFor(c => c.Headcount).InclusiveBetween(1, 1000).WithMessage("Kişi sayısı 1 ile 1000 arasında olmalı.");
        RuleFor(c => c.Reason).IsInEnum().WithMessage("Talep nedenini seçmelisiniz.");
        RuleFor(c => c.Urgency).IsInEnum().WithMessage("Aciliyeti seçmelisiniz.");
        RuleFor(c => c.ReplacedEmployeeName)
            .NotEmpty().WithMessage("Yerine alım yapılacak çalışanın adını yazmalısınız.")
            .When(c => c.Reason == HiringReason.Replacement);
        RuleFor(c => c.ReplacedEmployeeName)
            .MaximumLength(200).WithMessage("Çalışan adı en fazla 200 karakter olabilir.");
        // Tüm alanlar zorunludur (yerine alınan çalışan adı yalnızca "yerine alım" nedeninde).
        RuleFor(c => c.ExpectedStartDate).NotNull().WithMessage("Beklenen başlangıç tarihini seçmelisiniz.");
        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("Açıklamayı yazmalısınız.")
            .MaximumLength(4000).WithMessage("Açıklama en fazla 4000 karakter olabilir.");
        RuleFor(c => c.Competencies)
            .Must(c => c is not null && c.Any(x => !string.IsNullOrWhiteSpace(x)))
            .WithMessage("En az bir yetkinlik eklemelisiniz.")
            .Must(c => c is null || c.Count <= MaxCompetencies)
            .WithMessage($"En fazla {MaxCompetencies} yetkinlik ekleyebilirsiniz.");
        RuleForEach(c => c.Competencies)
            .MaximumLength(50).WithMessage("Bir yetkinlik en fazla 50 karakter olabilir.")
            .OverridePropertyName(nameof(IHiringRequestFields.Competencies));
    }
}

internal static class HiringRequestFieldsExtensions
{
    public static HiringRequestDetails ToDetails(this IHiringRequestFields fields) =>
        new(
            fields.Title,
            fields.DepartmentId,
            fields.LevelId,
            fields.Headcount,
            fields.Reason,
            fields.ReplacedEmployeeName,
            fields.Urgency,
            fields.ExpectedStartDate,
            fields.Competencies ?? [],
            fields.Description);

    /// <summary>Seçilen pozisyon adı, birim ve seviye geçerli değilse alan hatası döner; geçerliyse <c>null</c>.</summary>
    public static async Task<Error?> FindInvalidSelection(
        this IAppDbContext db, IHiringRequestFields fields, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        // Pozisyon adı serbest metin değil, yönetim ekranındaki "Pozisyon adları" listesinden seçilir.
        var title = fields.Title.Trim();
        if (!await db.DictionaryItems.AnyAsync(
                d => d.Type == DictionaryType.PositionTitle && d.IsActive && d.Name == title, ct))
            errors["title"] = ["Listeden geçerli bir pozisyon adı seçmelisiniz."];
        if (!await db.IsSelectable(fields.DepartmentId, DictionaryType.Department, ct))
            errors["departmentId"] = ["Geçerli bir birim seçmelisiniz."];
        if (!await db.IsSelectable(fields.LevelId, DictionaryType.Level, ct))
            errors["levelId"] = ["Geçerli bir seviye seçmelisiniz."];

        return errors.Count == 0 ? null : Error.Validation(errors);
    }

    public static Task<HiringRequest?> FindWithSteps(this IAppDbContext db, Guid id, CancellationToken ct) =>
        db.HiringRequests.Include(r => r.ApprovalSteps).FirstOrDefaultAsync(r => r.Id == id, ct);
}
