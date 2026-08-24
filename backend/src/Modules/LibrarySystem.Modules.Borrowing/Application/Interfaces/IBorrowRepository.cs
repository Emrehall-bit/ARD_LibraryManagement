using LibrarySystem.Modules.Borrowing.Domain;
using LibrarySystem.Modules.Borrowing.Application.Models;

namespace LibrarySystem.Modules.Borrowing.Application.Interfaces;

public interface IBorrowRepository
{
    Task<BorrowRecord?> GetActiveByUserIdAndBookIdAsync(
        string userId,
        Guid bookId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BorrowRecord>> GetActiveByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BorrowRecord>> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> HasOverdueBorrowsAsync(
        string userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task AcquireActiveBorrowLimitLockAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<BorrowRecordPage> GetOverduePageAsync(
        int page,
        int pageSize,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        BorrowRecord borrowRecord,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        BorrowRecord borrowRecord,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
