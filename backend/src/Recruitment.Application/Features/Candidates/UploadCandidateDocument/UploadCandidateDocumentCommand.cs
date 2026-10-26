using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Application.Features.Candidates.UploadCandidateDocument;

/// <param name="Content">Baştan okunabilir (seekable) dosya içeriği.</param>
/// <param name="TypeId">Dosya türü (sözlükte DocumentType); verilmezse tür belirtilmemiş, yani CV sayılır.</param>
public sealed record UploadCandidateDocumentCommand(
    Guid CandidateId, string FileName, long Size, Stream Content, Guid? TypeId = null) : ICommand<Guid>;

internal sealed class UploadCandidateDocumentCommandHandler(
    IAppDbContext db,
    IFileStorage storage,
    IOptions<FileStorageOptions> options,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<UploadCandidateDocumentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UploadCandidateDocumentCommand command, CancellationToken ct)
    {
        var inspection = await DocumentRules.Inspect(command.FileName, command.Size, command.Content, options.Value, ct);
        if (inspection.IsFailure)
            return inspection.Error;

        var candidate = await db.FindCandidate(command.CandidateId, ct);
        if (candidate is null)
            return CandidateErrors.NotFound;

        if (command.TypeId is { } typeId && !await db.IsSelectable(typeId, DictionaryType.DocumentType, ct))
            return Error.Validation(new Dictionary<string, string[]> { ["typeId"] = ["Geçerli bir dosya türü seçmelisiniz."] });

        var type = inspection.Value;
        var storageKey = await storage.Save(command.Content, type.Extension, ct);

        var document = candidate.AddDocument(
            FileNameOnly(command.FileName), type.ContentType, command.Size, storageKey, command.TypeId, currentUser.UserId, clock);

        await db.SaveChangesAsync(ct);
        return document.Id;
    }

    // İstemcinin gönderdiği klasör yolu atılır. Path.GetFileName sunucunun işletim sistemine göre
    // çalıştığı için Windows'tan gelen "C:\belgeler\cv.pdf" yolunu Linux'ta ayıramaz; elle ayrılır.
    private static string FileNameOnly(string fileName) => fileName[(fileName.LastIndexOfAny(['/', '\\']) + 1)..];
}
