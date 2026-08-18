using FluentValidation;

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
}
