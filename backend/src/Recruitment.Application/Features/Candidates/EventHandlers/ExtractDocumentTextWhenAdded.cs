using Recruitment.Application.Abstractions;
using Recruitment.Domain.Candidates.Events;

namespace Recruitment.Application.Features.Candidates.EventHandlers;

/// <summary>
/// Yüklenen CV'nin metnini çıkarıp adayın aranabilir metnine ekler. Yükleme isteğini bekletmemek
/// için arka planda (outbox) çalışır; aday, CV içeriğiyle birkaç saniye sonra aranabilir olur.
/// </summary>
internal sealed class ExtractDocumentTextWhenAdded(IAppDbContext db, IFileStorage storage, ITextExtractor extractor)
    : IDomainEventHandler<CandidateDocumentAdded>
{
    public async Task Handle(CandidateDocumentAdded domainEvent, CancellationToken ct)
    {
        var candidate = await db.FindCandidate(domainEvent.CandidateId, ct);
        var document = candidate?.Documents.FirstOrDefault(d => d.Id == domainEvent.DocumentId);
        if (candidate is null || document is null)
            return;

        await using var content = await storage.Open(document.StorageKey, ct);
        var text = await extractor.Extract(content, Path.GetExtension(document.FileName), ct);

        candidate.SetDocumentText(document.Id, text);
        await db.SaveChangesAsync(ct);
    }
}
