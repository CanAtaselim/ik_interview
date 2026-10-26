using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates;
using Recruitment.Application.Features.Candidates.CreateDocumentDownloadLink;

namespace Recruitment.Application.Features.CvReviews.CreateReviewDocumentLink;

/// <summary>
/// Değerlendirmeyi görebilen kullanıcı (ör. CV'nin önerildiği takım lideri) için adayın dosyasına
/// kısa süreli indirme bağlantısı üretir ve erişimi kayda geçirir.
/// </summary>
public sealed record CreateReviewDocumentLinkCommand(Guid ReviewId, Guid DocumentId) : ICommand<DownloadLinkDto>;

internal sealed class CreateReviewDocumentLinkCommandHandler(
    IAppDbContext db,
    IDownloadTokenService tokens,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<CreateReviewDocumentLinkCommand, DownloadLinkDto>
{
    public async Task<Result<DownloadLinkDto>> Handle(CreateReviewDocumentLinkCommand command, CancellationToken ct)
    {
        var candidateId = await db.VisibleCvReviews(currentUser)
            .Where(r => r.Id == command.ReviewId)
            .Select(r => (Guid?)r.CandidateId)
            .FirstOrDefaultAsync(ct);
        if (candidateId is null)
            return CvReviewErrors.NotFound;

        return await db.CreateRestrictedDocumentLink(
            tokens, currentUser.UserId, candidateId.Value, command.DocumentId, clock, ct);
    }
}
