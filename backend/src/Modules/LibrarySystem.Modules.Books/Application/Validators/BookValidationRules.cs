using FluentValidation;
using LibrarySystem.Modules.Books.Domain;

namespace LibrarySystem.Modules.Books.Application.Validators;

internal static class BookValidationRules
{
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

    public static bool IsSupportedCategory(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            Enum.TryParse<BookCategory>(value.Trim(), ignoreCase: true, out _);
    }
}
