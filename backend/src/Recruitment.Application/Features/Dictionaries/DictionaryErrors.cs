using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Dictionaries;

public static class DictionaryErrors
{
    public static readonly Error NotFound = Error.NotFound("Dictionary.NotFound", "Sözlük öğesi bulunamadı.");

    public static readonly Error Duplicate =
        Error.Conflict("Dictionary.Duplicate", "Bu adla bir öğe zaten var. Farklı bir ad yazın.");

    public static readonly Error SystemItem = Error.BusinessRule(
        "Dictionary.SystemItem",
        "Bu öğe sistem tarafından kullanılıyor; adı değiştirilemez ve silinemez. Gerekirse pasif yapabilirsiniz.");

    public static Error InUse(string action, IReadOnlyList<DictionaryUsageDto> usages) => Error.Conflict(
        "Dictionary.InUse",
        $"Bu öğe {string.Join(", ", usages.Select(u => $"{u.Count} {u.Area}"))} kullanılıyor; {action}. " +
        "Önce bu kayıtları silin veya başka bir değerle değiştirin. Listelerde görünmesini istemiyorsanız pasif yapabilirsiniz.",
        "usages",
        usages);
}
