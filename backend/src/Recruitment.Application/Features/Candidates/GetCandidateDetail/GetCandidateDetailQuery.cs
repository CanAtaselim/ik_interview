using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Features.Candidates.GetCandidateDetail;

public sealed record GetCandidateDetailQuery(Guid Id) : IQuery<CandidateDetailDto>;
