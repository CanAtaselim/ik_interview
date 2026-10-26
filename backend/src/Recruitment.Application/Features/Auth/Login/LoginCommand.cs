using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Features.Auth.Login;

public sealed record LoginCommand(string Username, string Password, string? IpAddress) : ICommand<AuthSession>;
