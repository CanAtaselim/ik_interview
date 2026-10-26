using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Features.Positions.GetPositionDetail;

public sealed record GetPositionDetailQuery(Guid Id) : IQuery<PositionDetailDto>;
