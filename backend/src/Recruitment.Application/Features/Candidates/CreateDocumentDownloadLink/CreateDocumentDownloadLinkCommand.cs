using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Audit;

namespace Recruitment.Application.Features.Candidates.CreateDocumentDownloadLink;

public sealed record DownloadLinkDto(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Yetkili kullanıcı için kısa süreli indirme bağlantısı üretir ve erişimi kayda geçirir.
/// Kayıt yazdığı için sorgu değil command'dır.
/// </summary>
public sealed record CreateDocumentDownloadLinkCommand(Guid CandidateId, Guid DocumentId) : ICommand<DownloadLinkDto>;

internal sealed class CreateDocumentDownloadLinkCommandHandler(
    IAppDbContext db,
    IDownloadTokenService tokens,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<CreateDocumentDownloadLinkCommand, DownloadLinkDto>
{
    public async Task<Result<DownloadLinkDto>> Handle(CreateDocumentDownloadLinkCommand command, CancellationToken ct)
    {
        var exists = await db.Candidates
            .Where(c => c.Id == command.CandidateId)
            .SelectMany(c => c.Documents)
            .AnyAsync(d => d.Id == command.DocumentId, ct);
        if (!exists)
            return CandidateErrors.DocumentNotFound;

        db.AccessLogs.Add(AccessLog.CvDownloaded(
            currentUser.UserId, command.CandidateId, command.DocumentId, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = tokens.Create(command.DocumentId);
        return new DownloadLinkDto(token, expiresAt);
    }
}
