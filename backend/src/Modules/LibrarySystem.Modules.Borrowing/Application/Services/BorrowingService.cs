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
    ICurrentUser currentUser,
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

            var borrowRecord = new BorrowRecord(Guid.NewGuid(), userId, bookId, DateTime.UtcNow);

            await borrowRepository.AddAsync(borrowRecord, transactionCancellationToken);
            await borrowRepository.SaveChangesAsync(transactionCancellationToken);

            return MapToResponseDto(borrowRecord);
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

            borrowRecord.Return(DateTime.UtcNow);
            await borrowRepository.UpdateAsync(borrowRecord, transactionCancellationToken);
            await bookInventoryService.IncreaseStockAsync(bookId, transactionCancellationToken);
            await borrowRepository.SaveChangesAsync(transactionCancellationToken);

            return MapToResponseDto(borrowRecord);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<BorrowRecordResponseDto>> GetMyBooksAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var borrowRecords = await borrowRepository.GetActiveByUserIdAsync(userId, cancellationToken);

        return borrowRecords.Select(MapToResponseDto).ToList();
    }

    private string GetCurrentUserId()
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new AuthenticationFailedException("Authenticated user is required.");
        }

        return currentUser.UserId;
    }

    private static BorrowRecordResponseDto MapToResponseDto(BorrowRecord borrowRecord)
    {
        return new BorrowRecordResponseDto(
            borrowRecord.Id,
            borrowRecord.BookId,
            borrowRecord.BorrowedAt,
            borrowRecord.ReturnedAt);
    }

}
