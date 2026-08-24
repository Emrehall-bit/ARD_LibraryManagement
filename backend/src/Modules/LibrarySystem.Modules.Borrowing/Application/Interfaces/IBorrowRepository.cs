using LibrarySystem.Modules.Borrowing.Domain;

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

    Task AddAsync(
        BorrowRecord borrowRecord,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        BorrowRecord borrowRecord,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
