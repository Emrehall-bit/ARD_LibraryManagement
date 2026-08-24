using LibrarySystem.Modules.Books.Application.Contracts;
using LibrarySystem.Modules.Borrowing.Application.Dtos;
using LibrarySystem.Modules.Borrowing.Application.Interfaces;
using LibrarySystem.Modules.Borrowing.Domain;
using LibrarySystem.Shared.Authentication;
using LibrarySystem.Shared.Exceptions;

namespace LibrarySystem.Modules.Borrowing.Application.Services;

internal sealed class BorrowingService(
    IBorrowRepository borrowRepository,
    IBookInventoryService bookInventoryService,
    IBookLookupService bookLookupService,
    ICurrentUser currentUser,
    IBorrowingClock clock,
    IBorrowingTransactionCoordinator transactionCoordinator) : IBorrowingService
{
    public async Task<BorrowRecordResponseDto> BorrowBookAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        return await transactionCoordinator.ExecuteAsync(async transactionCancellationToken =>
        {
            var bookInventory = await bookInventoryService.GetInventoryAsync(bookId, transactionCancellationToken);
            if (bookInventory is null)
            {
                throw new NotFoundException($"Book with id '{bookId}' was not found.");
            }

            if (bookInventory.Stock <= 0)
            {
                throw new BusinessException($"Book with id '{bookId}' is out of stock.");
            }

            if (await borrowRepository.GetActiveByUserIdAndBookIdAsync(
                    userId,
                    bookId,
                    transactionCancellationToken) is not null)
            {
                throw new BusinessException($"Book with id '{bookId}' is already borrowed by the current user.");
            }

            await bookInventoryService.DecreaseStockAsync(bookId, transactionCancellationToken);

            var borrowedAt = clock.UtcNow;
            var borrowRecord = new BorrowRecord(Guid.NewGuid(), userId, bookId, borrowedAt);

            await borrowRepository.AddAsync(borrowRecord, transactionCancellationToken);
            await borrowRepository.SaveChangesAsync(transactionCancellationToken);

            return MapToResponseDto(borrowRecord, clock.UtcNow);
        }, cancellationToken);
    }

    public async Task<BorrowRecordResponseDto> ReturnBookAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        return await transactionCoordinator.ExecuteAsync(async transactionCancellationToken =>
        {
            var borrowRecord = await borrowRepository.GetActiveByUserIdAndBookIdAsync(
                userId,
                bookId,
                transactionCancellationToken);

            if (borrowRecord is null)
            {
                throw new NotFoundException($"Active borrow record for book id '{bookId}' was not found.");
            }

            borrowRecord.Return(clock.UtcNow);
            await borrowRepository.UpdateAsync(borrowRecord, transactionCancellationToken);
            await bookInventoryService.IncreaseStockAsync(bookId, transactionCancellationToken);
            await borrowRepository.SaveChangesAsync(transactionCancellationToken);

            return MapToResponseDto(borrowRecord, clock.UtcNow);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<BorrowRecordResponseDto>> GetMyBooksAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var borrowRecords = await borrowRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        var bookIds = borrowRecords
            .Select(borrowRecord => borrowRecord.BookId)
            .Distinct()
            .ToArray();

        var books = await bookLookupService.GetByIdsAsync(bookIds, cancellationToken);
        var booksById = books.ToDictionary(book => book.Id);

        return MapToResponseDtos(borrowRecords, booksById);
    }

    public async Task<IReadOnlyList<BorrowRecordResponseDto>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var borrowRecords = await borrowRepository.GetByUserIdAsync(userId, cancellationToken);
        var bookIds = borrowRecords
            .Select(borrowRecord => borrowRecord.BookId)
            .Distinct()
            .ToArray();

        var books = await bookLookupService.GetByIdsAsync(bookIds, cancellationToken);
        var booksById = books.ToDictionary(book => book.Id);

        return MapToResponseDtos(borrowRecords, booksById);
    }

    private string GetCurrentUserId()
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new AuthenticationFailedException("Authenticated user is required.");
        }

        return currentUser.UserId;
    }

    private IReadOnlyList<BorrowRecordResponseDto> MapToResponseDtos(
        IReadOnlyList<BorrowRecord> borrowRecords,
        IReadOnlyDictionary<Guid, BookLookupItem> booksById)
    {
        var utcNow = clock.UtcNow;

        return borrowRecords
            .Select(borrowRecord => MapToResponseDto(
                borrowRecord,
                utcNow,
                booksById.GetValueOrDefault(borrowRecord.BookId)))
            .ToList();
    }

    private static BorrowRecordResponseDto MapToResponseDto(BorrowRecord borrowRecord, DateTime utcNow)
    {
        return MapToResponseDto(borrowRecord, utcNow, book: null);
    }

    private static BorrowRecordResponseDto MapToResponseDto(
        BorrowRecord borrowRecord,
        DateTime utcNow,
        BookLookupItem? book)
    {
        return new BorrowRecordResponseDto(
            borrowRecord.Id,
            borrowRecord.BookId,
            book?.Name,
            book?.Author,
            borrowRecord.BorrowedAt,
            borrowRecord.DueDate,
            borrowRecord.ReturnedAt,
            borrowRecord.GetStatus(utcNow).ToString());
    }
}
