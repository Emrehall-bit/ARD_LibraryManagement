using FluentValidation;
using LibrarySystem.Modules.Identity.Application.Dtos;

namespace LibrarySystem.Modules.Identity.Application.Validators;

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Username)
            .NotEmpty();

        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(8);
    }
}
