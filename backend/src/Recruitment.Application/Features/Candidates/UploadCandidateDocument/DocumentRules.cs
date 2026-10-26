using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Candidates.UploadCandidateDocument;

internal sealed record DocumentType(string Extension, string ContentType);

/// <summary>
/// Yüklenen dosyanın kabul koşulları: boyut, uzantı ve içeriğin uzantıyla uyuşması. İçerik türü
/// istemcinin beyanından değil, doğrulanan uzantıdan belirlenir.
/// </summary>
internal static class DocumentRules
{
    private static readonly Dictionary<string, (string ContentType, byte[] Signature)> KnownTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ("application/pdf", "%PDF"u8.ToArray()),
            [".docx"] = (
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50, 0x4B, 0x03, 0x04]),
            [".doc"] = ("application/msword", [0xD0, 0xCF, 0x11, 0xE0]),
            // Sertifika gibi taranmış belgeler için.
            [".png"] = ("image/png", [0x89, 0x50, 0x4E, 0x47]),
            [".jpg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
            [".jpeg"] = ("image/jpeg", [0xFF, 0xD8, 0xFF]),
        };

    public static async Task<Result<DocumentType>> Inspect(
        string fileName, long size, Stream content, FileStorageOptions options, CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName);
        var allowed = options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        if (!allowed || !KnownTypes.TryGetValue(extension, out var type))
            return CandidateErrors.InvalidFile("Yalnızca PDF, Word (DOC, DOCX) veya resim (PNG, JPG) dosyası yükleyebilirsiniz.");

        if (size <= 0)
            return CandidateErrors.InvalidFile("Boş dosya yüklenemez.");
        if (size > options.MaxFileSizeBytes)
            return CandidateErrors.InvalidFile($"Dosya en fazla {options.MaxFileSizeMb} MB olabilir.");

        var header = new byte[type.Signature.Length];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        content.Position = 0;
        if (read < header.Length || !header.AsSpan().SequenceEqual(type.Signature))
            return CandidateErrors.InvalidFile("Dosyanın içeriği uzantısıyla uyuşmuyor. Dosyayı kontrol edip tekrar yükleyin.");

        return new DocumentType(extension.ToLowerInvariant(), type.ContentType);
    }
}
