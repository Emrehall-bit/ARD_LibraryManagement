using FluentValidation;
using LibrarySystem.Modules.Identity.Application.Dtos;

namespace LibrarySystem.Modules.Identity.Application.Validators;

internal sealed class GetAdminUsersQueryValidator : AbstractValidator<GetAdminUsersQueryDto>
{
    public GetAdminUsersQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
