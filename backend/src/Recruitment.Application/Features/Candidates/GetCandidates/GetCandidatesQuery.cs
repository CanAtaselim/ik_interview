using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Candidates.GetCandidates;

/// <param name="Search">Ad, iletişim, etiket, ön görüşme metni ve CV içeriğinde aranır.</param>
/// <param name="InTalentPool">Verilirse yalnızca havuzdakiler (true) veya havuzda olmayanlar (false).</param>
/// <param name="Process">Sürece göre süzgeç (aşağıdaki <see cref="CandidateProcessFilter"/>).</param>
/// <param name="Blacklisted">Verilirse yürürlükteki kara liste kaydı olanlar (true) veya olmayanlar (false).</param>
/// <param name="City">Şehir adında, büyük/küçük harf ayırmadan aranır.</param>
public sealed record GetCandidatesQuery(
    string? Search,
    Guid? SourceId,
    bool? InTalentPool,
    string? Tag,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize,
    CandidateProcessFilter? Process = null,
    bool? Blacklisted = null,
    string? City = null) : IQuery<PagedResult<CandidateListItemDto>>;

public enum CandidateProcessFilter
{
    /// <summary>Açık (devam eden) süreci olan.</summary>
    Active,

    /// <summary>Hiçbir pozisyonda süreci olmayan.</summary>
    None,

    /// <summary>İşe alınmış süreci olan.</summary>
    Hired,

    /// <summary>Reddedilmiş veya çekilmiş süreci olan.</summary>
    Closed,
}
