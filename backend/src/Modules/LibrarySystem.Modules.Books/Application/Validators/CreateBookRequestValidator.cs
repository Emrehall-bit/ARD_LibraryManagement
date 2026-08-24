using FluentValidation;
using LibrarySystem.Modules.Books.Application.Dtos;

namespace LibrarySystem.Modules.Books.Application.Validators;

internal sealed class CreateBookRequestValidator : AbstractValidator<CreateBookRequestDto>
{
    public CreateBookRequestValidator()
    {
        RuleFor(request => request.Name)
            .ApplyBookNameRules();

        RuleFor(request => request.Author)
            .ApplyBookAuthorRules();

        RuleFor(request => request.Stock)
            .ApplyBookStockRules();

        RuleFor(request => request.Category)
            .ApplyBookCategoryRules();
    }
}
