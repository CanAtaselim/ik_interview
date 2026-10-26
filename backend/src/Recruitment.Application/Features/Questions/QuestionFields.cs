using FluentValidation;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Questions;

namespace Recruitment.Application.Features.Questions;

/// <summary>Soru ekleme ve düzenleme command'larının ortak alanları.</summary>
public interface IQuestionFields
{
    Guid DepartmentId { get; }
    string? PositionTitle { get; }
    Guid? LevelId { get; }
    Guid CompetencyId { get; }
    QuestionType Type { get; }
    int Difficulty { get; }
    decimal Weight { get; }
    string Text { get; }
    string? Guidance { get; }
}

internal abstract class QuestionFieldsValidator<T> : AbstractValidator<T>
    where T : IQuestionFields
{
    protected QuestionFieldsValidator()
    {
        RuleFor(c => c.DepartmentId).NotEmpty().WithMessage("Sorunun ait olduğu birimi seçmelisiniz.");
        RuleFor(c => c.CompetencyId).NotEmpty().WithMessage("Sorunun ölçtüğü yetkinliği seçmelisiniz.");
        RuleFor(c => c.Type).IsInEnum().WithMessage("Soru tipini seçmelisiniz.");
        RuleFor(c => c.Difficulty).InclusiveBetween(1, 3).WithMessage("Zorluk 1 (kolay), 2 (orta) veya 3 (zor) olmalı.");
        RuleFor(c => c.Weight)
            .InclusiveBetween(Question.MinWeight, Question.MaxWeight)
            .WithMessage($"Ağırlık {Question.MinWeight} ile {Question.MaxWeight} arasında olmalı.");
        RuleFor(c => c.Text)
            .NotEmpty().WithMessage("Soru metnini yazmalısınız.")
            .MaximumLength(Question.TextMaxLength).WithMessage($"Soru en fazla {Question.TextMaxLength} karakter olabilir.");
        RuleFor(c => c.Guidance)
            .MaximumLength(Question.GuidanceMaxLength)
            .WithMessage($"Beklenen cevap en fazla {Question.GuidanceMaxLength} karakter olabilir.");
        RuleFor(c => c.PositionTitle)
            .MaximumLength(Question.PositionTitleMaxLength)
            .WithMessage($"Pozisyon adı en fazla {Question.PositionTitleMaxLength} karakter olabilir.");
    }
}

internal static class QuestionFieldChecks
{
    public static QuestionContent ToContent(this IQuestionFields fields) => new(
        fields.DepartmentId, fields.PositionTitle, fields.LevelId, fields.CompetencyId, fields.Type,
        fields.Difficulty, fields.Weight, fields.Text, fields.Guidance);

    public static async Task<Error?> FindInvalidSelections(this IAppDbContext db, IQuestionFields fields, CancellationToken ct)
    {
        if (!await db.IsSelectable(fields.DepartmentId, DictionaryType.Department, ct))
            return Field("departmentId", "Geçerli bir birim seçmelisiniz.");
        if (!await db.IsSelectable(fields.CompetencyId, DictionaryType.Competency, ct))
            return Field("competencyId", "Geçerli bir yetkinlik seçmelisiniz.");
        if (fields.LevelId is { } levelId && !await db.IsSelectable(levelId, DictionaryType.Level, ct))
            return Field("levelId", "Geçerli bir seviye seçmelisiniz.");
        return null;
    }

    public static Error Field(string field, string message) =>
        Error.Validation(new Dictionary<string, string[]> { [field] = [message] });
}
