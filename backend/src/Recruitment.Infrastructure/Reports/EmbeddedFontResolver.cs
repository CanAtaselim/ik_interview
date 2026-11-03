using System.Reflection;
using PdfSharp.Fonts;

namespace Recruitment.Infrastructure.Reports;

/// <summary>
/// PDF yazı tipi çözümleyicisi. Noto Sans (SIL Open Font License) dosyaya gömülüdür; böylece sunucuda
/// (IIS ya da Linux konteyner) yazı tipi kurulu olmasa da Türkçe karakterler (ş, ğ, İ, ı…) doğru çıkar.
/// </summary>
internal sealed class EmbeddedFontResolver : IFontResolver
{
    public const string FamilyName = "Noto Sans";

    private static readonly Assembly Assembly = typeof(EmbeddedFontResolver).Assembly;
    private static readonly object Gate = new();
    private static bool _installed;

    /// <summary>Çözümleyiciyi bir kez kurar; PDFsharp'ın yazı tipi ayarı süreç geneldir.</summary>
    public static void Install()
    {
        lock (Gate)
        {
            if (_installed)
                return;

            GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
            _installed = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? "NotoSans-Bold" : isItalic ? "NotoSans-Italic" : "NotoSans-Regular");

    public byte[]? GetFont(string faceName)
    {
        var resource = Assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith($".{faceName}.ttf", StringComparison.OrdinalIgnoreCase));
        if (resource is null)
            return null;

        using var stream = Assembly.GetManifestResourceStream(resource)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
