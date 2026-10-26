using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.HiringRequests.SubmitHiringRequest;

public sealed record SubmitHiringRequestCommand(Guid Id) : ICommand<Unit>;
