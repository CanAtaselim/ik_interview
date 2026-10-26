using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Candidates;

public static class CandidateErrors
{
    public const string DuplicatesKey = "duplicates";
    public const string BlacklistMatchesKey = "blacklistMatches";

    public static readonly Error NotFound = Error.NotFound("Candidate.NotFound", "Aday bulunamadı.");

    public static readonly Error DocumentNotFound = Error.NotFound("Candidate.DocumentNotFound", "Dosya bulunamadı.");

    public static readonly Error ScreeningNotFound =
        Error.NotFound("Candidate.ScreeningNotFound", "Ön görüşme kaydı bulunamadı.");

    public static readonly Error DownloadLinkExpired = Error.NotFound(
        "Candidate.DownloadLinkExpired", "İndirme bağlantısının süresi dolmuş. Dosyayı aday kartından yeniden indirin.");

    /// <summary>Aynı e-posta veya telefonla aday var; İK onaylarsa yine de kaydedilir.</summary>
    public static Error PossibleDuplicate(IReadOnlyList<DuplicateCandidateDto> duplicates) => Error.Conflict(
        "Candidate.PossibleDuplicate",
        "Aynı e-posta veya telefonla kayıtlı aday var. Mevcut kaydı kontrol edin; yine de kaydetmek isterseniz onaylayın.",
        DuplicatesKey,
        duplicates);

    /// <summary>Kişi kara listedeki biriyle eşleşiyor; İK onaylarsa yine de kaydedilir ve onay kayda geçer.</summary>
    public static Error BlacklistMatch(IReadOnlyList<Abstractions.BlacklistMatch> matches) => Error.Conflict(
        "Candidate.BlacklistMatch",
        "Bu kişi kara listedeki bir kayıtla eşleşiyor. Kaydı kontrol edin; yine de kaydetmek isterseniz onaylayın.",
        BlacklistMatchesKey,
        matches);

    public static Error InvalidFile(string message) =>
        Error.Validation(new Dictionary<string, string[]> { ["file"] = [message] });
}
