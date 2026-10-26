using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates.UploadCandidateDocument;

namespace Recruitment.Application.Features.Candidates.ExtractCvProfile;

/// <summary>
/// Yüklenen CV'den aday ekleme formunu önceden doldurmak için bilgileri okur. Dosya saklanmaz.
/// Okunamayan dosya veya alan hata sayılmaz; bulunanlar döner, bulunamayan boş kalır.
/// </summary>
/// <param name="Content">Baştan okunabilir (seekable) dosya içeriği.</param>
public sealed record ExtractCvProfileQuery(string FileName, long Size, Stream Content) : IQuery<CvProfile>;

internal sealed class ExtractCvProfileQueryHandler(ITextExtractor extractor, IOptions<FileStorageOptions> options)
    : IQueryHandler<ExtractCvProfileQuery, CvProfile>
{
    public async Task<Result<CvProfile>> Handle(ExtractCvProfileQuery query, CancellationToken ct)
    {
        var inspection = await DocumentRules.Inspect(query.FileName, query.Size, query.Content, options.Value, ct);
        if (inspection.IsFailure)
            return CvProfile.Empty;

        var extension = inspection.Value.Extension;
        if (extension is not (".pdf" or ".docx" or ".doc"))
            return CvProfile.Empty;

        var lines = await extractor.ExtractLines(query.Content, extension, ct);
        // Eski .doc'tan yalnızca metin parçaları gelir; ad tahmini güvenilir olmadığı için yapılmaz.
        return CvProfileParser.Parse(lines, detectName: extension != ".doc");
    }
}
