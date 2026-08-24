using FluentValidation;
using LibrarySystem.Modules.Identity.Application.Dtos;
using LibrarySystem.Modules.Identity.Application.Interfaces;
using LibrarySystem.Shared.Borrowing;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Modules.Identity.Infrastructure.Services;

internal sealed class AdminUserService(
    IdentityDbContext dbContext,
    IUserBorrowingSummaryService userBorrowingSummaryService,
    IValidator<GetAdminUsersQueryDto> getAdminUsersQueryValidator) : IAdminUserService
{
    public async Task<PagedAdminUsersResponseDto> GetUsersAsync(
        GetAdminUsersQueryDto query,
        CancellationToken cancellationToken = default)
    {
        await getAdminUsersQueryValidator.ValidateAndThrowAsync(query, cancellationToken);

        var usersQuery = dbContext.Users.AsNoTracking();
        var trimmedSearch = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : query.Search.Trim();

        if (trimmedSearch is not null)
        {
            var normalizedSearch = trimmedSearch.ToUpperInvariant();
            usersQuery = usersQuery.Where(user =>
                (user.NormalizedUserName != null && user.NormalizedUserName.Contains(normalizedSearch)) ||
                (user.NormalizedEmail != null && user.NormalizedEmail.Contains(normalizedSearch)));
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);
        var users = await usersQuery
            .OrderBy(user => user.UserName)
            .ThenBy(user => user.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(user => new AdminUserListItem(
                user.Id,
                user.Id.ToString(),
                user.UserName ?? user.Id.ToString(),
                user.Email))
            .ToListAsync(cancellationToken);

        var userIds = users.Select(user => user.Id).ToArray();
        var userIdValues = users.Select(user => user.IdValue).ToArray();
        var roles = await GetRolesByUserIdAsync(userIds, cancellationToken);
        var summaries = await userBorrowingSummaryService.GetByUserIdsAsync(userIdValues, cancellationToken);
        var summariesByUserId = summaries.ToDictionary(
            summary => summary.UserId,
            StringComparer.OrdinalIgnoreCase);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        return new PagedAdminUsersResponseDto(
            users
                .Select(user =>
                {
                    summariesByUserId.TryGetValue(user.IdValue, out var summary);

                    return new AdminUserResponseDto(
                        user.IdValue,
                        user.Username,
                        user.Email,
                        roles.GetValueOrDefault(user.Id) ?? [],
                        summary?.ActiveBorrowCount ?? 0,
                        summary?.OverdueBorrowCount ?? 0);
                })
                .ToList(),
            query.Page,
            query.PageSize,
            totalCount,
            totalPages);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetRolesByUserIdAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var userRoles = await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userIds.Contains(userRole.UserId)
                orderby role.Name
                select new
                {
                    userRole.UserId,
                    RoleName = role.Name
                })
            .ToListAsync(cancellationToken);

        return userRoles
            .GroupBy(userRole => userRole.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(userRole => userRole.RoleName)
                    .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
                    .Select(roleName => roleName!)
                    .ToList());
    }

    private sealed record AdminUserListItem(
        Guid Id,
        string IdValue,
        string Username,
        string? Email);
}
