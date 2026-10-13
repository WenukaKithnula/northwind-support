using FluentValidation;
using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    // The limits keep a sign-in from being used to make the server hash something enormous.
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);

        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}
