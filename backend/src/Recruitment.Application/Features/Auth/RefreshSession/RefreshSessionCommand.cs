using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Features.Auth.RefreshSession;

public sealed record RefreshSessionCommand(string? RefreshToken, string? IpAddress) : ICommand<AuthSession>;
