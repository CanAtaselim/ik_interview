using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Features.HiringRequests.GetHiringRequestDetail;

public sealed record GetHiringRequestDetailQuery(Guid Id) : IQuery<HiringRequestDetailDto>;
