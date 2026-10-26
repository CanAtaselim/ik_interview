using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Positions;

namespace Recruitment.Application.Features.Positions.GetPositions;

public sealed record GetPositionsQuery(
    PositionStatus? Status,
    string? Search,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<PositionListItemDto>>;
