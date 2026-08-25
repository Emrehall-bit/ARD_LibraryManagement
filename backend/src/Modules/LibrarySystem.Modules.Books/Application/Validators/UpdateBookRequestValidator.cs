using FluentValidation;
using LibrarySystem.Modules.Books.Application.Dtos;

namespace LibrarySystem.Modules.Books.Application.Validators;

internal sealed class UpdateBookRequestValidator : AbstractValidator<UpdateBookRequestDto>
{
    public UpdateBookRequestValidator()
    {
        RuleFor(request => request.Name)
            .ApplyBookNameRules();

        RuleFor(request => request.Author)
            .ApplyBookAuthorRules();

        RuleFor(request => request.Stock)
            .ApplyBookStockRules();

        RuleFor(request => request.Category)
            .ApplyBookCategoryRules();

        RuleFor(request => request.Description)
            .ApplyBookDescriptionRules();

        RuleFor(request => request.Isbn)
            .ApplyBookIsbnRules();

        RuleFor(request => request.Publisher)
            .ApplyBookPublisherRules();

        RuleFor(request => request.PublishedYear)
            .ApplyBookPublishedYearRules();
    }
}
