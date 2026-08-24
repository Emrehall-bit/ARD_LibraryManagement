using LibrarySystem.Modules.Borrowing.Application.Interfaces;
using LibrarySystem.Shared.Borrowing;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Modules.Borrowing.Infrastructure.Services;

internal sealed class AdminBorrowingDashboardSummaryService(
    BorrowingDbContext dbContext,
    IBorrowingClock clock) : IAdminBorrowingDashboardSummaryService
{
    public async Task<AdminBorrowingDashboardSummary> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var utcNow = clock.UtcNow;
        var activeBorrows = await dbContext.BorrowRecords
            .AsNoTracking()
            .CountAsync(borrowRecord => borrowRecord.ReturnedAt == null, cancellationToken);
        var overdueBorrows = await dbContext.BorrowRecords
            .AsNoTracking()
            .CountAsync(
                borrowRecord =>
                    borrowRecord.ReturnedAt == null &&
                    borrowRecord.DueDate < utcNow,
                cancellationToken);
        var returnedBorrows = await dbContext.BorrowRecords
            .AsNoTracking()
            .CountAsync(borrowRecord => borrowRecord.ReturnedAt != null, cancellationToken);
        var recentOverdueBorrows = await dbContext.BorrowRecords
            .AsNoTracking()
            .Where(borrowRecord =>
                borrowRecord.ReturnedAt == null &&
                borrowRecord.DueDate < utcNow)
            .OrderBy(borrowRecord => borrowRecord.DueDate)
            .ThenBy(borrowRecord => borrowRecord.Id)
            .Take(5)
            .ToListAsync(cancellationToken);

        return new AdminBorrowingDashboardSummary(
            activeBorrows,
            overdueBorrows,
            returnedBorrows,
            recentOverdueBorrows
                .Select(borrowRecord => new AdminRecentOverdueBorrow(
                    borrowRecord.Id,
                    borrowRecord.UserId,
                    borrowRecord.BookId,
                    borrowRecord.DueDate,
                    borrowRecord.GetOverdueDays(utcNow)))
                .ToList());
    }
}
