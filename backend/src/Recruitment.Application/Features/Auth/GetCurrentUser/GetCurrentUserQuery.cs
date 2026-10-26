using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Features.Auth.GetCurrentUser;

public sealed record GetCurrentUserQuery : IQuery<CurrentUserDto>;
