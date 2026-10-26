using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Candidates.OpenCandidateDocument;

public sealed record DocumentContent(Stream Content, string FileName, string ContentType);

/// <summary>İndirme token'ıyla dosyayı açar. Yetki, token üretilirken kontrol edilmiştir.</summary>
public sealed record OpenCandidateDocumentQuery(string Token) : IQuery<DocumentContent>;

internal sealed class OpenCandidateDocumentQueryHandler(
    IAppDbContext db, IDownloadTokenService tokens, IFileStorage storage)
    : IQueryHandler<OpenCandidateDocumentQuery, DocumentContent>
{
    public async Task<Result<DocumentContent>> Handle(OpenCandidateDocumentQuery query, CancellationToken ct)
    {
        if (tokens.Validate(query.Token) is not { } documentId)
            return CandidateErrors.DownloadLinkExpired;

        // Bağlantı üretildikten sonra kaldırılan dosya da (soft delete filtresi sayesinde) açılmaz.
        var document = await db.Candidates.AsNoTracking()
            .SelectMany(c => c.Documents)
            .Where(d => d.Id == documentId)
            .Select(d => new { d.StorageKey, d.FileName, d.ContentType })
            .FirstOrDefaultAsync(ct);
        if (document is null)
            return CandidateErrors.DocumentNotFound;

        var content = await storage.Open(document.StorageKey, ct);
        return new DocumentContent(content, document.FileName, document.ContentType);
    }
}
