using FluentValidation;
using LibrarySystem.Modules.Books.Application.Dtos;

namespace LibrarySystem.Modules.Books.Application.Validators;

internal sealed class CreateBookRequestValidator : AbstractValidator<CreateBookRequestDto>
{
    public CreateBookRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Author)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Stock)
            .GreaterThanOrEqualTo(0);
    }
}
