using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Candidates.DeleteCandidateDocument;

/// <summary>
/// Dosyayı aday kartından kaldırır. Kayıt soft delete edilir ve dosyanın kendisi depolamada kalır:
/// KVKK saklama/silme kuralları netleşene kadar hiçbir veri geri dönüşsüz yok edilmez.
/// </summary>
public sealed record DeleteCandidateDocumentCommand(Guid CandidateId, Guid DocumentId) : ICommand<Unit>;

internal sealed class DeleteCandidateDocumentCommandHandler(IAppDbContext db)
    : ICommandHandler<DeleteCandidateDocumentCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteCandidateDocumentCommand command, CancellationToken ct)
    {
        var candidate = await db.FindCandidate(command.CandidateId, ct);
        if (candidate is null)
            return CandidateErrors.NotFound;
        if (candidate.Documents.All(d => d.Id != command.DocumentId))
            return CandidateErrors.DocumentNotFound;

        candidate.RemoveDocument(command.DocumentId);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
