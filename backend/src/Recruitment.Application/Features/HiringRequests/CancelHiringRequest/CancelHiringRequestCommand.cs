using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.HiringRequests.CancelHiringRequest;

public sealed record CancelHiringRequestCommand(Guid Id) : ICommand<Unit>;
