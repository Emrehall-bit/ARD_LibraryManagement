using FluentValidation;
using LibrarySystem.Modules.Borrowing.Application.Dtos;

namespace LibrarySystem.Modules.Borrowing.Application.Validators;

internal sealed class GetBorrowHistoryQueryValidator : AbstractValidator<GetBorrowHistoryQueryDto>
{
    public GetBorrowHistoryQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
