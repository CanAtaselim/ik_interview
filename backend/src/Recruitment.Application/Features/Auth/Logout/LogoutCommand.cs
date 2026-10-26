using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Auth.Logout;

public sealed record LogoutCommand(string? RefreshToken) : ICommand<Unit>;
