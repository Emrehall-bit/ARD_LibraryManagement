using FluentValidation;
using LibrarySystem.Modules.Identity.Application.Dtos;

namespace LibrarySystem.Modules.Identity.Application.Validators;

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Username)
            .NotEmpty();

        RuleFor(request => request.Password)
            .NotEmpty();
    }
}
