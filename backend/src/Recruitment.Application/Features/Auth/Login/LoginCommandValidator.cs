using FluentValidation;

namespace Recruitment.Application.Features.Auth.Login;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Username)
            .NotEmpty().WithMessage("Kullanıcı adınızı girmelisiniz.")
            .MaximumLength(256).WithMessage("Kullanıcı adı en fazla 256 karakter olabilir.");

        RuleFor(c => c.Password)
            .NotEmpty().WithMessage("Şifrenizi girmelisiniz.");
    }
}
