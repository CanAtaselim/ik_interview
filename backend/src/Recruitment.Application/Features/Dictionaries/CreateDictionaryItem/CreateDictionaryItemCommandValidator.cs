using FluentValidation;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Application.Features.Dictionaries.CreateDictionaryItem;

internal sealed class CreateDictionaryItemCommandValidator : AbstractValidator<CreateDictionaryItemCommand>
{
    public CreateDictionaryItemCommandValidator()
    {
        RuleFor(c => c.Type).IsInEnum().WithMessage("Geçerli bir sözlük türü seçmelisiniz.");
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Bir ad yazmalısınız.")
            .MaximumLength(DictionaryItem.NameMaxLength)
            .WithMessage($"Ad en fazla {DictionaryItem.NameMaxLength} karakter olabilir.");
    }
}
