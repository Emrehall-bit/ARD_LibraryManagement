using LibrarySystem.Modules.Borrowing.Application.Interfaces;
using LibrarySystem.Modules.Borrowing.Application.Models;
using LibrarySystem.Modules.Borrowing.Domain;
using LibrarySystem.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Modules.Borrowing.Infrastructure.Repositories;

internal sealed class BorrowRepository(BorrowingDbContext dbContext) : IBorrowRepository
{
    public async Task<BorrowRecord?> GetActiveByUserIdAndBookIdAsync(
        string userId,
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BorrowRecords
            .FirstOrDefaultAsync(
                borrowRecord =>
                    borrowRecord.UserId == userId &&
                    borrowRecord.BookId == bookId &&
                    borrowRecord.ReturnedAt == null,
                cancellationToken);
    }

    public async Task<IReadOnlyList<BorrowRecord>> GetActiveByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BorrowRecords
            .AsNoTracking()
            .Where(borrowRecord =>
                borrowRecord.UserId == userId &&
                borrowRecord.ReturnedAt == null)
            .OrderByDescending(borrowRecord => borrowRecord.BorrowedAt)
            .ThenBy(borrowRecord => borrowRecord.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<BorrowRecordPage> GetPageByUserIdAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.BorrowRecords
            .AsNoTracking()
            .Where(borrowRecord => borrowRecord.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(borrowRecord => borrowRecord.BorrowedAt)
            .ThenBy(borrowRecord => borrowRecord.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new BorrowRecordPage(items, page, pageSize, totalCount);
    }

    public async Task<bool> HasOverdueBorrowsAsync(
        string userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BorrowRecords
            .AsNoTracking()
            .AnyAsync(
                borrowRecord =>
                    borrowRecord.UserId == userId &&
                    borrowRecord.ReturnedAt == null &&
                    borrowRecord.DueDate < utcNow,
                cancellationToken);
    }

    public async Task AcquireActiveBorrowLimitLockAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var lockKey = $"library-system:borrowing:active-limit:{userId}";

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0));",
            cancellationToken);
    }

    public async Task<int> CountActiveByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BorrowRecords
            .AsNoTracking()
            .CountAsync(
                borrowRecord =>
                    borrowRecord.UserId == userId &&
                    borrowRecord.ReturnedAt == null,
                cancellationToken);
    }

    public async Task<BorrowRecordPage> GetOverduePageAsync(
        int page,
        int pageSize,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.BorrowRecords
            .AsNoTracking()
            .Where(borrowRecord =>
                borrowRecord.ReturnedAt == null &&
                borrowRecord.DueDate < utcNow);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(borrowRecord => borrowRecord.DueDate)
            .ThenBy(borrowRecord => borrowRecord.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new BorrowRecordPage(items, page, pageSize, totalCount);
    }

    public async Task AddAsync(
        BorrowRecord borrowRecord,
        CancellationToken cancellationToken = default)
    {
        await dbContext.BorrowRecords.AddAsync(borrowRecord, cancellationToken);
    }

    public Task UpdateAsync(
        BorrowRecord borrowRecord,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dbContext.BorrowRecords.Update(borrowRecord);

        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "The borrow record could not be updated because it was changed concurrently.",
                exception);
        }
    }
}
