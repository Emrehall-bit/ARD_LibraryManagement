using FluentValidation;
using LibrarySystem.Modules.Books.Domain;

namespace LibrarySystem.Modules.Books.Application.Validators;

internal static class BookValidationRules
{
    public const int DescriptionMaxLength = 4000;
    public const int IsbnMaxLength = 32;
    public const int PublisherMaxLength = 200;

    public static IRuleBuilderOptions<T, string> ApplyBookNameRules<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .MaximumLength(200);
    }

    public static IRuleBuilderOptions<T, string> ApplyBookAuthorRules<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .MaximumLength(200);
    }

    public static IRuleBuilderOptions<T, int> ApplyBookStockRules<T>(
        this IRuleBuilder<T, int> ruleBuilder)
    {
        return ruleBuilder.GreaterThanOrEqualTo(0);
    }

    public static IRuleBuilderOptions<T, string> ApplyBookCategoryRules<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .Must(IsSupportedCategory)
            .WithMessage("'Category' must be a supported book category.");
    }

    public static IRuleBuilderOptions<T, string?> ApplyBookDescriptionRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .MaximumLength(DescriptionMaxLength);
    }

    public static IRuleBuilderOptions<T, string?> ApplyBookIsbnRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .MaximumLength(IsbnMaxLength);
    }

    public static IRuleBuilderOptions<T, string?> ApplyBookPublisherRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .MaximumLength(PublisherMaxLength);
    }

    public static IRuleBuilderOptions<T, int?> ApplyBookPublishedYearRules<T>(
        this IRuleBuilder<T, int?> ruleBuilder)
    {
        return ruleBuilder
            .Must(year => year is null || year.Value > 0 && year.Value <= DateTime.UtcNow.Year)
            .WithMessage("'Published Year' must be a positive year no later than the current year.");
    }

    public static bool IsSupportedCategory(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            Enum.TryParse<BookCategory>(value.Trim(), ignoreCase: true, out _);
    }
}
