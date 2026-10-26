namespace Recruitment.Application.Abstractions;

public enum BlacklistMatchKind
{
    /// <summary>E-posta veya telefon aynı.</summary>
    Exact,

    /// <summary>Yalnızca ad soyad benziyor; farklı bir kişi olabilir.</summary>
    Possible,
}

public sealed record BlacklistMatch(
    Guid EntryId, Guid CandidateId, string FullName, string ReasonName, BlacklistMatchKind Kind);

public interface IBlacklistMatcher
{
    /// <summary>
    /// Verilen kişiyle eşleşen, yürürlükteki kara liste kayıtları: e-posta veya telefon (mükerrer
    /// kuralıyla aynı sadeleştirme) birebir, ad soyad ise benzerlikle eşleştirilir.
    /// </summary>
    Task<IReadOnlyList<BlacklistMatch>> FindMatches(
        string? email, string? phone, string? firstName, string? lastName, Guid? excludeCandidateId, CancellationToken ct);
}
