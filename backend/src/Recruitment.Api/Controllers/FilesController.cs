using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Candidates.OpenCandidateDocument;

namespace Recruitment.Api.Controllers;

[ApiController]
[Route("api/v1/files")]
public sealed class FilesController(IDispatcher dispatcher) : ControllerBase
{
    private static readonly HashSet<string> InlineContentTypes =
        new(["application/pdf", "image/png", "image/jpeg"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// İmzalı bağlantıyla dosya indirir. Tarayıcı bu adresi yeni sekmede, Authorization başlığı
    /// olmadan açar; yetki, kısa ömürlü token üretilirken kontrol edilmiştir.
    /// </summary>
    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(string token, [FromQuery] bool inline, CancellationToken ct)
    {
        var result = await dispatcher.Query(new OpenCandidateDocumentQuery(token), ct);
        if (result.IsFailure)
            return result.Error.ToProblemResult(this);

        // Tarayıcı içerik türünü kendisi tahmin etmesin; dosya paylaşılan önbelleklerde tutulmasın.
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";

        var document = result.Value;

        // Tarayıcıda yan sekmede açılması istenen PDF ve resimler satır içi gönderilir; Word gibi
        // tarayıcının gösteremediği dosyalar (veya "inline" istenmediyse hepsi) indirilir.
        if (inline && InlineContentTypes.Contains(document.ContentType))
        {
            var disposition = new ContentDispositionHeaderValue("inline") { FileNameStar = document.FileName };
            Response.Headers.ContentDisposition = disposition.ToString();
            return File(document.Content, document.ContentType);
        }

        return File(document.Content, document.ContentType, document.FileName);
    }
}
