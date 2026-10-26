using Recruitment.Application.Abstractions;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Application.Features.Dictionaries.GetDictionaryItems;

/// <param name="IncludeInactive">Yönetim ekranı pasif öğeleri de görür; seçim listeleri görmez.</param>
public sealed record GetDictionaryItemsQuery(DictionaryType Type, bool IncludeInactive)
    : IQuery<IReadOnlyList<DictionaryItemDto>>;
