using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Application.Features.Questions;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Questions;

namespace Recruitment.Application.Features.InterviewTemplates;

public interface IInterviewTemplateFields
{
    string Name { get; }
    Guid DepartmentId { get; }
    Guid? LevelId { get; }
    Guid? InterviewTypeId { get; }
    IReadOnlyList<Guid> QuestionIds { get; }
}

internal abstract class InterviewTemplateFieldsValidator<T> : AbstractValidator<T>
    where T : IInterviewTemplateFields
{
    protected InterviewTemplateFieldsValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Şablonun adını yazmalısınız.")
            .MaximumLength(InterviewTemplate.NameMaxLength)
            .WithMessage($"Ad en fazla {InterviewTemplate.NameMaxLength} karakter olabilir.");
        RuleFor(c => c.DepartmentId).NotEmpty().WithMessage("Şablonun ait olduğu birimi seçmelisiniz.");
        RuleFor(c => c.QuestionIds)
            .NotEmpty().WithMessage("Şablona en az bir soru eklemelisiniz.")
            .Must(ids => ids is null || ids.Distinct().Count() <= InterviewTemplate.MaxQuestions)
            .WithMessage($"Bir şablonda en fazla {InterviewTemplate.MaxQuestions} soru olabilir.");
    }
}

internal static class InterviewTemplateChecks
{
    /// <summary>
    /// Seçimler geçerli mi? Sorular kullanıcının görebildiği sorular olmalı; şablonda zaten
    /// bulunanlar dışında pasif soru eklenemez.
    /// </summary>
    public static async Task<Error?> FindInvalidTemplate(
        this IAppDbContext db, ICurrentUser user, IInterviewTemplateFields fields, IReadOnlyCollection<Guid> existingQuestionIds, CancellationToken ct)
    {
        if (user.DepartmentError(fields.DepartmentId) is { } forbidden)
            return forbidden;
        if (!await db.IsSelectable(fields.DepartmentId, DictionaryType.Department, ct))
            return QuestionFieldChecks.Field("departmentId", "Geçerli bir birim seçmelisiniz.");
        if (fields.LevelId is { } levelId && !await db.IsSelectable(levelId, DictionaryType.Level, ct))
            return QuestionFieldChecks.Field("levelId", "Geçerli bir seviye seçmelisiniz.");
        if (fields.InterviewTypeId is { } typeId && !await db.IsSelectable(typeId, DictionaryType.InterviewType, ct))
            return QuestionFieldChecks.Field("interviewTypeId", "Geçerli bir mülakat tipi seçmelisiniz.");

        var ids = fields.QuestionIds.Distinct().ToList();
        var usable = await db.VisibleQuestions(user)
            .Where(q => ids.Contains(q.Id) && (q.IsActive || existingQuestionIds.Contains(q.Id)))
            .CountAsync(ct);
        return usable == ids.Count
            ? null
            : QuestionFieldChecks.Field("questionIds", "Şablona yalnızca görebildiğiniz aktif sorular eklenebilir.");
    }

    public static IQueryable<InterviewTemplateListItemDto> ToListItems(
        this IQueryable<InterviewTemplate> templates, IAppDbContext db, ICurrentUser user)
    {
        var dictionary = db.DictionaryItems;
        var isHr = user.IsHr;
        var ownDepartment = user.DepartmentId;

        return templates.Select(t => new InterviewTemplateListItemDto(
            t.Id,
            t.Name,
            t.DepartmentId,
            dictionary.Where(d => d.Id == t.DepartmentId).Select(d => d.Name).FirstOrDefault() ?? string.Empty,
            t.LevelId,
            dictionary.Where(d => d.Id == t.LevelId).Select(d => d.Name).FirstOrDefault(),
            t.InterviewTypeId,
            dictionary.Where(d => d.Id == t.InterviewTypeId).Select(d => d.Name).FirstOrDefault(),
            t.IsActive,
            t.Questions.Count,
            isHr || (ownDepartment != null && t.DepartmentId == ownDepartment)));
    }
}
