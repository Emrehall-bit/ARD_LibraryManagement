using FluentValidation;
using LibrarySystem.Modules.Books.Application.Dtos;

namespace LibrarySystem.Modules.Books.Application.Validators;

internal sealed class GetBooksQueryValidator : AbstractValidator<GetBooksQueryDto>
{
    private static readonly string[] SupportedSortFields = ["name", "author", "stock"];
    private static readonly string[] SupportedSortDirections = ["asc", "desc"];
    private static readonly string[] SupportedStockStatuses = ["all", "inStock", "outOfStock"];

    public GetBooksQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.SortBy)
            .Must(value => IsSupportedValue(value, SupportedSortFields))
            .WithMessage("'Sort By' must be one of: name, author, stock.");

        RuleFor(query => query.SortDirection)
            .Must(value => IsSupportedValue(value, SupportedSortDirections))
            .WithMessage("'Sort Direction' must be one of: asc, desc.");

        RuleFor(query => query.StockStatus)
            .Must(value => IsSupportedValue(value, SupportedStockStatuses))
            .WithMessage("'Stock Status' must be one of: all, inStock, outOfStock.");

        RuleFor(query => query.Category)
            .Must(value => value is null || BookValidationRules.IsSupportedCategory(value))
            .WithMessage("'Category' must be a supported book category.");
    }

    private static bool IsSupportedValue(string? value, IReadOnlyCollection<string> supportedValues)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            supportedValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
