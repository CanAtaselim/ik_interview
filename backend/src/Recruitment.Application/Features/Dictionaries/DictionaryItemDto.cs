namespace Recruitment.Application.Features.Dictionaries;

public sealed record DictionaryItemDto(Guid Id, string Name, int SortOrder, bool IsActive);
