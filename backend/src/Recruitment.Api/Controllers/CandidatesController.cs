using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates.CreateCandidate;
using Recruitment.Application.Features.Candidates.CreateDocumentDownloadLink;
using Recruitment.Application.Features.Candidates.DeleteCandidateDocument;
using Recruitment.Application.Features.Candidates.ExtractCvProfile;
using Recruitment.Application.Features.Candidates.FindDuplicateCandidates;
using Recruitment.Application.Features.Candidates.GetCandidateDetail;
using Recruitment.Application.Features.Candidates.GetCandidates;
using Recruitment.Application.Features.Candidates.GetCandidateTimeline;
using Recruitment.Application.Features.Candidates.SetTalentPool;
using Recruitment.Application.Features.Candidates.DeleteScreening;
using Recruitment.Application.Features.Candidates.StartScreening;
using Recruitment.Application.Features.Candidates.UpdateCandidate;
using Recruitment.Application.Features.Candidates.UpdateScreening;
using Recruitment.Application.Features.Candidates.UploadCandidateDocument;

namespace Recruitment.Api.Controllers;

/// <summary>
/// Aday havuzu. Şimdilik yalnızca İK erişir; takım liderinin kendisine önerilen adayı kısıtlı
/// görmesi CV önerme modülüyle gelecek.
/// </summary>
[ApiController]
[Route("api/v1/candidates")]
[Authorize(Policy = Policies.HrOnly)]
public sealed class CandidatesController(IDispatcher dispatcher) : ControllerBase
{
    // Dosya sınırının (FileStorage:MaxFileSizeMb) biraz üstü; asıl kontrol handler'dadır.
    private const long MaxUploadRequestBytes = 12 * 1024 * 1024;

    public sealed record TalentPoolRequest(bool InTalentPool);

    public sealed record DownloadLinkResponse(string Url, DateTimeOffset ExpiresAt);

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search,
        [FromQuery] Guid? sourceId,
        [FromQuery] bool? inTalentPool,
        [FromQuery] string? tag,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        [FromQuery] CandidateProcessFilter? process = null,
        [FromQuery] bool? blacklisted = null,
        [FromQuery] string? city = null)
    {
        var query = new GetCandidatesQuery(search, sourceId, inTalentPool, tag, page, pageSize, process, blacklisted, city);
        return (await dispatcher.Query(query, ct)).ToActionResult(this);
    }

    /// <summary>
    /// CV'den ad, e-posta, telefon ve bağlantıları okuyup aday ekleme formu için önerir. Dosya saklanmaz;
    /// okunamayan dosya veya alan için hata dönmez, ilgili alanlar boş gelir.
    /// </summary>
    [HttpPost("cv-prefill")]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    public async Task<IActionResult> PrefillFromCv(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
            return Ok(CvProfile.Empty);

        await using var content = file.OpenReadStream();
        return (await dispatcher.Query(new ExtractCvProfileQuery(file.FileName, file.Length, content), ct)).ToActionResult(this);
    }

    [HttpGet("duplicates")]
    public async Task<IActionResult> FindDuplicates(
        [FromQuery] string? email, [FromQuery] string? phone, [FromQuery] Guid? excludeId, CancellationToken ct) =>
        (await dispatcher.Query(new FindDuplicateCandidatesQuery(email, phone, excludeId), ct)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct) =>
        (await dispatcher.Query(new GetCandidateDetailQuery(id), ct)).ToActionResult(this);

    [HttpGet("{id:guid}/timeline")]
    public async Task<IActionResult> GetTimeline(
        Guid id, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        (await dispatcher.Query(new GetCandidateTimelineQuery(id, page, pageSize), ct)).ToActionResult(this);

    [HttpPost]
    public async Task<IActionResult> Create(CreateCandidateCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command, ct)).ToCreatedResult(this);

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCandidateCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command with { Id = id }, ct)).ToActionResult(this);

    [HttpPost("{id:guid}/talent-pool")]
    public async Task<IActionResult> SetTalentPool(Guid id, TalentPoolRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new SetTalentPoolCommand(id, request.InTalentPool), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    public async Task<IActionResult> UploadDocument(
        Guid id, IFormFile? file, [FromForm] Guid? typeId, CancellationToken ct)
    {
        if (file is null)
        {
            var missing = Error.Validation(new Dictionary<string, string[]> { ["file"] = ["Yüklenecek dosyayı seçmelisiniz."] });
            return missing.ToProblemResult(this);
        }

        await using var content = file.OpenReadStream();
        var command = new UploadCandidateDocumentCommand(id, file.FileName, file.Length, content, typeId);
        return (await dispatcher.Send(command, ct)).ToCreatedResult(this);
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id, Guid documentId, CancellationToken ct) =>
        (await dispatcher.Send(new DeleteCandidateDocumentCommand(id, documentId), ct)).ToActionResult(this);

    /// <summary>Dosya için birkaç dakika geçerli indirme bağlantısı üretir ve erişimi kayda geçirir.</summary>
    [HttpPost("{id:guid}/documents/{documentId:guid}/download-link")]
    public async Task<IActionResult> CreateDownloadLink(Guid id, Guid documentId, CancellationToken ct)
    {
        var result = await dispatcher.Send(new CreateDocumentDownloadLinkCommand(id, documentId), ct);
        if (result.IsFailure)
            return result.Error.ToProblemResult(this);

        var url = Url.Action(nameof(FilesController.Download), "Files", new { token = result.Value.Token })!;
        return Ok(new DownloadLinkResponse(url, result.Value.ExpiresAt));
    }

    [HttpPost("{id:guid}/screenings")]
    public async Task<IActionResult> StartScreening(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new StartScreeningCommand(id), ct)).ToCreatedResult(this);

    [HttpPut("{id:guid}/screenings/{screeningId:guid}")]
    public async Task<IActionResult> UpdateScreening(
        Guid id, Guid screeningId, UpdateScreeningCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command with { CandidateId = id, ScreeningId = screeningId }, ct)).ToActionResult(this);

    [HttpDelete("{id:guid}/screenings/{screeningId:guid}")]
    public async Task<IActionResult> DeleteScreening(Guid id, Guid screeningId, CancellationToken ct) =>
        (await dispatcher.Send(new DeleteScreeningCommand(id, screeningId), ct)).ToActionResult(this);
}
