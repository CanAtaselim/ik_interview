using Recruitment.Application.Abstractions;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Application.Features.Dictionaries.CreateDictionaryItem;

public sealed record CreateDictionaryItemCommand(DictionaryType Type, string Name) : ICommand<Guid>;
