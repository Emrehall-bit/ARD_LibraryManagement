using LibrarySystem.Modules.Identity.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Modules.Identity.Infrastructure.Services;

internal sealed class UserDirectory(IdentityDbContext dbContext) : IUserDirectory
{
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .AsNoTracking()
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserDirectoryItem>> GetByIdsAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var distinctUserIds = userIds
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Select(userId => userId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(userId => Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : (Guid?)null)
            .OfType<Guid>()
            .ToArray();

        if (distinctUserIds.Length == 0)
        {
            return [];
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => distinctUserIds.Contains(user.Id))
            .Select(user => new UserDirectoryItem(
                user.Id.ToString(),
                user.UserName ?? user.Id.ToString()))
            .ToListAsync(cancellationToken);
    }
}
