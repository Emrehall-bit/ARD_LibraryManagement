using LibrarySystem.Modules.Borrowing.Application.Interfaces;
using LibrarySystem.Shared.Borrowing;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Modules.Borrowing.Infrastructure.Services;

internal sealed class UserBorrowingSummaryService(
    BorrowingDbContext dbContext,
    IBorrowingClock clock) : IUserBorrowingSummaryService
{
    public async Task<IReadOnlyList<UserBorrowingSummary>> GetByUserIdsAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var distinctUserIds = userIds
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Select(userId => userId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinctUserIds.Length == 0)
        {
            return [];
        }

        var utcNow = clock.UtcNow;

        return await dbContext.BorrowRecords
            .AsNoTracking()
            .Where(borrowRecord =>
                distinctUserIds.Contains(borrowRecord.UserId) &&
                borrowRecord.ReturnedAt == null)
            .GroupBy(borrowRecord => borrowRecord.UserId)
            .Select(group => new UserBorrowingSummary(
                group.Key,
                group.Count(),
                group.Count(borrowRecord => borrowRecord.DueDate < utcNow)))
            .ToListAsync(cancellationToken);
    }
}
