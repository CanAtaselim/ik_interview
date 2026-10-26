namespace Recruitment.Domain.Common;

/// <summary>
/// Arama için metin sadeleştirme. Hem aranan metin hem aranacak içerik aynı kuraldan geçer;
/// böylece sonuç veritabanının dil ayarına bağlı kalmaz ve Türkçedeki İ/I/ı/i ayrımı aramayı bozmaz.
/// </summary>
public static class SearchText
{
    /// <summary>Kırpar, küçültür ve tüm i türevlerini "i" yapar: "IŞIK" → "işik".</summary>
    public static string Fold(string text) =>
        text.Trim().Replace('İ', 'i').Replace('I', 'i').ToLowerInvariant().Replace('ı', 'i');

    /// <summary>Metni sadeleştirip harf/rakam dizilerine böler: "Ali.Veli@x.com" → ali, veli, x, com.</summary>
    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var tokens = new List<string>();
        var start = -1;
        var folded = Fold(text);

        for (var i = 0; i <= folded.Length; i++)
        {
            var isWordChar = i < folded.Length && char.IsLetterOrDigit(folded[i]);
            if (isWordChar && start < 0)
                start = i;
            else if (!isWordChar && start >= 0)
            {
                tokens.Add(folded[start..i]);
                start = -1;
            }
        }

        return tokens;
    }

    /// <summary>Verilen parçalardan, boşlukla ayrılmış sözcüklerden oluşan aranabilir metni üretir.</summary>
    public static string Build(IEnumerable<string?> parts, int maxLength)
    {
        var text = string.Join(' ', parts.SelectMany(Tokenize));
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
