using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates;
using Recruitment.Application.Features.Candidates.CreateDocumentDownloadLink;

namespace Recruitment.Application.Features.Interviews.CreateInterviewDocumentLink;

/// <summary>Mülakatı görebilen kullanıcı (mülakatçı, İK) için adayın dosyasına kısa süreli bağlantı.</summary>
public sealed record CreateInterviewDocumentLinkCommand(Guid InterviewId, Guid DocumentId) : ICommand<DownloadLinkDto>;

internal sealed class CreateInterviewDocumentLinkCommandHandler(
    IAppDbContext db, IDownloadTokenService tokens, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<CreateInterviewDocumentLinkCommand, DownloadLinkDto>
{
    public async Task<Result<DownloadLinkDto>> Handle(CreateInterviewDocumentLinkCommand command, CancellationToken ct)
    {
        var candidateId = await db.VisibleInterviews(currentUser)
            .Where(i => i.Id == command.InterviewId)
            .Select(i => (Guid?)i.CandidateId)
            .FirstOrDefaultAsync(ct);
        if (candidateId is null)
            return InterviewErrors.NotFound;

        return await db.CreateRestrictedDocumentLink(
            tokens, currentUser.UserId, candidateId.Value, command.DocumentId, clock, ct);
    }
}
