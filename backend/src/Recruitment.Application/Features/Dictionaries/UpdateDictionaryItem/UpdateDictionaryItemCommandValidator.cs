using FluentValidation;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Application.Features.Dictionaries.UpdateDictionaryItem;

internal sealed class UpdateDictionaryItemCommandValidator : AbstractValidator<UpdateDictionaryItemCommand>
{
    public UpdateDictionaryItemCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Bir ad yazmalısınız.")
            .MaximumLength(DictionaryItem.NameMaxLength)
            .WithMessage($"Ad en fazla {DictionaryItem.NameMaxLength} karakter olabilir.");
        RuleFor(c => c.SortOrder).GreaterThanOrEqualTo(0).WithMessage("Sıra 0 veya daha büyük olmalı.");
    }
}
