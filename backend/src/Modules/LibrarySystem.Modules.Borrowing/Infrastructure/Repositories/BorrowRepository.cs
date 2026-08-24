using LibrarySystem.Modules.Borrowing.Application.Interfaces;
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

    public async Task<IReadOnlyList<BorrowRecord>> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BorrowRecords
            .AsNoTracking()
            .Where(borrowRecord => borrowRecord.UserId == userId)
            .OrderByDescending(borrowRecord => borrowRecord.BorrowedAt)
            .ThenBy(borrowRecord => borrowRecord.Id)
            .ToListAsync(cancellationToken);
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
