using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Features.Notifications;

/// <summary>
/// Şablondaki <c>{Degisken}</c> alanlarını doldurur. Değerler gövdede HTML olarak kodlanır, böylece
/// aday adı gibi kullanıcı verisi e-postaya kod olarak giremez.
/// </summary>
internal sealed partial class EmailComposer(IAppDbContext db)
{
    public async Task<(string Subject, string Html)> Render(
        string code, IReadOnlyDictionary<string, string?> values, CancellationToken ct)
    {
        var stored = await db.EmailTemplates.AsNoTracking()
            .Where(t => t.Code == code)
            .Select(t => new { t.Subject, t.Body })
            .FirstOrDefaultAsync(ct);
        var fallback = DefaultEmailTemplates.Find(code)
            ?? throw new InvalidOperationException($"E-posta şablonu tanımlı değil: {code}");

        var subject = Fill(stored?.Subject ?? fallback.Subject, values, encode: false);
        var body = Fill(stored?.Body ?? fallback.Body, values, encode: true);
        return (subject, Layout(body));
    }

    /// <summary>Kaydedilmemiş bir metni verilen değerlerle işler (yönetim ekranındaki önizleme).</summary>
    public (string Subject, string Html) Preview(string subject, string body, IReadOnlyDictionary<string, string?> values) =>
        (Fill(subject, values, encode: false), Layout(Fill(body, values, encode: true)));

    private static string Fill(string template, IReadOnlyDictionary<string, string?> values, bool encode) =>
        Placeholder().Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value, out var value)
                ? encode ? Escape(value ?? string.Empty) : value ?? string.Empty
                : match.Value);

    // Yalnızca HTML'i bozan karakterler kodlanır; Türkçe harfler olduğu gibi kalır (WebUtility.HtmlEncode
    // Ç, Ö, Ü gibi harfleri &#199; biçimine çevirirdi ve düz metin olarak okunan e-postada bozuk görünürdü).
    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    private static string Layout(string body) =>
        $"""
        <!doctype html>
        <html lang="tr">
        <body style="margin:0;padding:24px;background:#f6f6fb;font-family:Segoe UI,Arial,sans-serif;color:#1d1f33;font-size:15px;line-height:1.6">
          <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e2e3ef;border-radius:10px;padding:24px">
        {body}
          </div>
          <p style="max-width:560px;margin:12px auto 0;color:#5d6079;font-size:12px">Bu e-posta İK İşe Alım uygulamasından otomatik gönderildi.</p>
        </body>
        </html>
        """;

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();
}
