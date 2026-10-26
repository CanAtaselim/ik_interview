using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.CvReviews.CreateReviewDocumentLink;
using Recruitment.Application.Features.CvReviews.DecideCvReview;
using Recruitment.Application.Features.CvReviews.GetCvReviewDetail;
using Recruitment.Application.Features.CvReviews.GetCvReviews;
using Recruitment.Application.Features.CvReviews.SuggestCandidate;
using Recruitment.Domain.CvReviews;

namespace Recruitment.Api.Controllers;

/// <summary>
/// CV önerme ve değerlendirme. Öneriyi İK yapar; kararı CV'nin önerildiği takım lideri verir.
/// Takım liderinin yalnızca kendisine veya birimine önerilenleri görmesi handler'da uygulanır.
/// </summary>
[ApiController]
[Route("api/v1/cv-reviews")]
[Authorize(Policy = Policies.CanReviewCv)]
public sealed class CvReviewsController(IDispatcher dispatcher) : ControllerBase
{
    public sealed record DecideRequest(CvReviewDecision Decision, string Reason, Guid? RejectionReasonId);

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] CvReviewStatusFilter? status,
        [FromQuery] bool mine,
        [FromQuery] Guid? candidateId,
        [FromQuery] string? search,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize)
    {
        var query = new GetCvReviewsQuery(status, mine, candidateId, search, page, pageSize);
        return (await dispatcher.Query(query, ct)).ToActionResult(this);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct) =>
        (await dispatcher.Query(new GetCvReviewDetailQuery(id), ct)).ToActionResult(this);

    [HttpPost]
    [Authorize(Policy = Policies.HrOnly)]
    public async Task<IActionResult> Suggest(SuggestCandidateCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command, ct)).ToActionResult(this);

    [HttpPost("{id:guid}/decide")]
    public async Task<IActionResult> Decide(Guid id, DecideRequest request, CancellationToken ct)
    {
        var command = new DecideCvReviewCommand(id, request.Decision, request.Reason, request.RejectionReasonId);
        return (await dispatcher.Send(command, ct)).ToActionResult(this);
    }

    /// <summary>Adayın dosyası için kısa süreli indirme bağlantısı (değerlendirmeyi görebilen kullanıcıya).</summary>
    [HttpPost("{id:guid}/documents/{documentId:guid}/download-link")]
    public async Task<IActionResult> CreateDocumentLink(Guid id, Guid documentId, CancellationToken ct)
    {
        var result = await dispatcher.Send(new CreateReviewDocumentLinkCommand(id, documentId), ct);
        if (result.IsFailure)
            return result.Error.ToProblemResult(this);

        var url = Url.Action(nameof(FilesController.Download), "Files", new { token = result.Value.Token })!;
        return Ok(new CandidatesController.DownloadLinkResponse(url, result.Value.ExpiresAt));
    }
}
