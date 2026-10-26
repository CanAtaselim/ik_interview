using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.HiringRequests;

namespace Recruitment.Application.Features.HiringRequests.GetHiringRequests;

/// <param name="AwaitingMyDecision">Yalnızca sıradaki onayı isteği yapan kullanıcının vereceği talepler.</param>
public sealed record GetHiringRequestsQuery(
    HiringRequestStatus? Status,
    string? Search,
    bool AwaitingMyDecision,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<HiringRequestListItemDto>>;
