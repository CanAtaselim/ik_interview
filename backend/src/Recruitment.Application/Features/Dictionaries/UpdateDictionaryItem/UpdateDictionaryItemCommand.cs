using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Dictionaries.UpdateDictionaryItem;

public sealed record UpdateDictionaryItemCommand(Guid Id, string Name, int SortOrder, bool IsActive) : ICommand<Unit>;
