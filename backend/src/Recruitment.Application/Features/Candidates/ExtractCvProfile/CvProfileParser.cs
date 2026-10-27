using System.Text.RegularExpressions;

namespace Recruitment.Application.Features.Candidates.ExtractCvProfile;

/// <summary>CV'den okunabilen aday bilgileri. Okunamayan alan <c>null</c> kalır.</summary>
public sealed record CvProfile(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    string? LinkedInUrl,
    string? GitHubUrl,
    string? City,
    IReadOnlyList<string> Skills)
{
    public static readonly CvProfile Empty = new(null, null, null, null, null, null, null, Array.Empty<string>());
}

/// <summary>
/// CV metninden ad soyad, e-posta, telefon, LinkedIn/GitHub adresi, il ve yetenekleri bulur. Tahmine dayanır:
/// emin olunamayan alan boş bırakılır, yanlış değer önerilmez.
/// </summary>
public static partial class CvProfileParser
{
    // Ad genellikle belgenin başındadır.
    private const int NameSearchLines = 12;
    private const int MaxNameLength = 40;

    private static readonly string[] NotNameMarkers =
    [
        "özgeçmiş", "ozgecmis", "curriculum", "resume", "cv", "profil", "profile", "kişisel", "kisisel", "iletişim",
        "iletisim", "contact", "hakkımda", "hakkimda", "about", "deneyim", "experience", "eğitim", "egitim", "education",
        "yetenek", "skills", "beceri", "referans", "sertifika", "proje", "summary", "özet", "ozet", "hedef", "objective",
        "yazılım", "yazilim", "geliştirici", "gelistirici", "developer", "engineer", "mühendis", "muhendis", "uzman",
        "manager", "müdür", "mudur", "yönetici", "yonetici", "analist", "analyst", "tasarımcı", "tasarimci", "designer",
        "specialist", "consultant", "danışman", "danisman", "stajyer", "intern", "architect", "mimar", "lead",
        "senior", "junior", "personal", "information", "sayfa", "page", "tel", "phone", "email", "mail", "adres", "address",
    ];

    public static CvProfile Parse(IReadOnlyList<string> lines, bool detectName = true)
    {
        var (first, last) = detectName ? FindName(lines) : (null, null);
        return new CvProfile(
            first, last, FindEmail(lines), FindPhone(lines), FindLinkedIn(lines), FindGitHub(lines), FindCity(lines),
            CvSkillExtractor.Extract(lines));
    }

    // Başlıktaki iletişim bloğu: ilk birkaç kısa satır.
    private const int HeaderLines = 10;
    private const int MaxHeaderLineLength = 70;

    private static readonly string[] AddressMarkers =
        ["adres", "address", "lokasyon", "location", "konum", "şehir", "sehir", "city", "ikamet", "yaşadığı", "yasadigi"];

    // Bu sözcüklerin geçtiği satırdaki il, adayın yaşadığı yer değildir (okul, işyeri, doğum yeri…).
    private static readonly string[] NotResidenceMarkers =
    [
        "üniversite", "universite", "university", "lise", "okul", "fakülte", "fakulte", "enstitü", "enstitu", "bölüm",
        "bolum", "college", "doğum", "dogum", "born", "a.ş", "ltd", "şti", "sti.", "bank", "holding", "company",
        "şirket", "sirket", "teknoloji", "technologies", "mezun", "stajyer", "intern",
    ];

    /// <summary>
    /// Adayın yaşadığı ili bulur: önce "Adres/Lokasyon/Şehir" gibi etiketli satırlar, yoksa başlıktaki
    /// kısa iletişim satırları. Okul, işyeri veya doğum yeri satırları sayılmaz.
    /// </summary>
    private static string? FindCity(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var lowered = TurkishLower(line);
            if (AddressMarkers.Any(marker => ContainsWord(lowered, marker)) && !HasNotResidence(lowered)
                && ProvinceIn(line) is { } labelled)
                return labelled;
        }

        foreach (var line in lines.Take(HeaderLines).Where(l => l.Length <= MaxHeaderLineLength))
        {
            if (!HasNotResidence(TurkishLower(line)) && ProvinceIn(line) is { } province)
                return province;
        }

        return null;
    }

    private static bool HasNotResidence(string lowered) => NotResidenceMarkers.Any(lowered.Contains);

    private static string? ProvinceIn(string line)
    {
        foreach (var token in Regex.Split(line, @"[^\p{L}]+").Where(t => t.Length >= 3))
        {
            if (Provinces.ByFolded.TryGetValue(Provinces.Fold(token), out var province))
                return province;
        }

        return null;
    }

    private static (string? First, string? Last) FindName(IReadOnlyList<string> lines)
    {
        foreach (var raw in lines.Take(NameSearchLines))
        {
            var line = raw.Trim().TrimEnd(',', ';', '|');
            if (line.Length is 0 or > MaxNameLength || !LooksLikeName(line))
                continue;

            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(TurkishTitleCase).ToList();
            return (string.Join(' ', words[..^1]), words[^1]);
        }

        return (null, null);
    }

    private static bool LooksLikeName(string line)
    {
        if (!NamePattern().IsMatch(line))
            return false;

        var lowered = TurkishLower(line);
        if (NotNameMarkers.Any(marker => ContainsWord(lowered, marker)))
            return false;

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // "Ali Veli" veya "ALİ VELİ"; "ali veli" gibi karışık yazılmış satırlar ad sayılmaz.
        return words.Length is >= 2 and <= 4 && words.All(w => char.IsUpper(w[0]));
    }

    private static bool ContainsWord(string lowered, string marker) =>
        lowered.Split(' ', '/', '-', ':').Any(word => word == marker || (marker.Length >= 5 && word.StartsWith(marker, StringComparison.Ordinal)));

    private static string? FindEmail(IReadOnlyList<string> lines) =>
        lines.Select(line => EmailPattern().Match(line)).FirstOrDefault(m => m.Success)?.Value.ToLowerInvariant();

    private static string? FindPhone(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            if (MobilePattern().Match(line) is { Success: true } mobile)
                return Format(mobile);
        }

        foreach (var line in lines)
        {
            if (LandlinePattern().Match(line) is { Success: true } landline)
                return Format(landline);
        }

        return null;

        static string Format(Match match) =>
            $"0{match.Groups[1].Value} {match.Groups[2].Value} {match.Groups[3].Value} {match.Groups[4].Value}";
    }

    private static string? FindLinkedIn(IReadOnlyList<string> lines)
    {
        var slug = lines.Select(line => LinkedInPattern().Match(line)).FirstOrDefault(m => m.Success)?.Groups[1].Value;
        return slug is null ? null : $"https://www.linkedin.com/in/{slug.TrimEnd('.', '-')}";
    }

    private static string? FindGitHub(IReadOnlyList<string> lines)
    {
        var user = lines.Select(line => GitHubPattern().Match(line)).FirstOrDefault(m => m.Success)?.Groups[1].Value;
        return user is null ? null : $"https://github.com/{user}";
    }

    // Türkçe büyük/küçük harf dönüşümü; kültür bilgisi olmayan (invariant) ortamlarda da doğru çalışır.
    private static string TurkishLower(string text) =>
        text.Replace('I', 'ı').Replace('İ', 'i').ToLowerInvariant();

    private static string TurkishTitleCase(string word)
    {
        var lower = TurkishLower(word);
        var first = lower[0] switch { 'i' => 'İ', 'ı' => 'I', var c => char.ToUpperInvariant(c) };
        return first + lower[1..];
    }

    [GeneratedRegex(@"^[\p{L}][\p{L}'’.\-]*(?: [\p{L}][\p{L}'’.\-]*){1,3}$")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<!\d)(?:\+?\s*90[\s\-.]*|0[\s\-.]*)?\(?\s*(5\d{2})\s*\)?[\s\-.]*(\d{3})[\s\-.]*(\d{2})[\s\-.]*(\d{2})(?!\d)")]
    private static partial Regex MobilePattern();

    [GeneratedRegex(@"(?<!\d)(?:\+?\s*90[\s\-.]*|0[\s\-.]*)?\(?\s*([2-4]\d{2})\s*\)?[\s\-.]*(\d{3})[\s\-.]*(\d{2})[\s\-.]*(\d{2})(?!\d)")]
    private static partial Regex LandlinePattern();

    [GeneratedRegex(@"linkedin\.com/in/([A-Za-z0-9_%\-.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex LinkedInPattern();

    [GeneratedRegex(@"github\.com/([A-Za-z0-9\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubPattern();
}

/// <summary>81 il; Türkçe karakterli veya ASCII yazılmış olsa da (Istanbul/İstanbul) eşleşir.</summary>
internal static class Provinces
{
    private static readonly string[] Names =
    [
        "Adana", "Adıyaman", "Afyonkarahisar", "Ağrı", "Aksaray", "Amasya", "Ankara", "Antalya", "Ardahan", "Artvin",
        "Aydın", "Balıkesir", "Bartın", "Batman", "Bayburt", "Bilecik", "Bingöl", "Bitlis", "Bolu", "Burdur", "Bursa",
        "Çanakkale", "Çankırı", "Çorum", "Denizli", "Diyarbakır", "Düzce", "Edirne", "Elazığ", "Erzincan", "Erzurum",
        "Eskişehir", "Gaziantep", "Giresun", "Gümüşhane", "Hakkâri", "Hatay", "Iğdır", "Isparta", "İstanbul", "İzmir",
        "Kahramanmaraş", "Karabük", "Karaman", "Kars", "Kastamonu", "Kayseri", "Kilis", "Kırıkkale", "Kırklareli",
        "Kırşehir", "Kocaeli", "Konya", "Kütahya", "Malatya", "Manisa", "Mardin", "Mersin", "Muğla", "Muş", "Nevşehir",
        "Niğde", "Ordu", "Osmaniye", "Rize", "Sakarya", "Samsun", "Siirt", "Sinop", "Sivas", "Şanlıurfa", "Şırnak",
        "Tekirdağ", "Tokat", "Trabzon", "Tunceli", "Uşak", "Van", "Yalova", "Yozgat", "Zonguldak",
    ];

    public static readonly IReadOnlyDictionary<string, string> ByFolded =
        Names.ToDictionary(Fold, name => name, StringComparer.Ordinal);

    /// <summary>Küçük harfe ve Türkçe karakterlerin ASCII karşılığına indirger.</summary>
    public static string Fold(string text)
    {
        var folded = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            folded.Append(c switch
            {
                'İ' or 'I' or 'ı' or 'i' => 'i',
                'Ş' or 'ş' => 's',
                'Ğ' or 'ğ' => 'g',
                'Ü' or 'ü' => 'u',
                'Ö' or 'ö' => 'o',
                'Ç' or 'ç' => 'c',
                'Â' or 'â' => 'a',
                _ => char.ToLowerInvariant(c),
            });
        }

        return folded.ToString();
    }
}
