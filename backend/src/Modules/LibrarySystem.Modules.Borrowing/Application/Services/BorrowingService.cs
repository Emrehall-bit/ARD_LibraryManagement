using FluentValidation;
using LibrarySystem.Modules.Books.Application.Contracts;
using LibrarySystem.Modules.Borrowing.Application.Dtos;
using LibrarySystem.Modules.Borrowing.Application.Interfaces;
using LibrarySystem.Modules.Borrowing.Domain;
using LibrarySystem.Modules.Identity.Application.Contracts;
using LibrarySystem.Shared.Authentication;
using LibrarySystem.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace LibrarySystem.Modules.Borrowing.Application.Services;

internal sealed class BorrowingService(
    IBorrowRepository borrowRepository,
    IBookInventoryService bookInventoryService,
    IBookLookupService bookLookupService,
    IUserDirectory userDirectory,
    ICurrentUser currentUser,
    IBorrowingClock clock,
    IBorrowingTransactionCoordinator transactionCoordinator,
    IBookStockChangeNotifier bookStockChangeNotifier,
    ILogger<BorrowingService> logger,
    IValidator<GetBorrowHistoryQueryDto> getBorrowHistoryQueryValidator,
    IValidator<GetOverdueBorrowRecordsQueryDto> getOverdueBorrowRecordsQueryValidator) : IBorrowingService
{
    public async Task<BorrowRecordResponseDto> BorrowBookAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        var result = await transactionCoordinator.ExecuteAsync(async transactionCancellationToken =>
        {
            var utcNow = clock.UtcNow;

            await borrowRepository.AcquireActiveBorrowLimitLockAsync(userId, transactionCancellationToken);

            if (await borrowRepository.HasOverdueBorrowsAsync(userId, utcNow, transactionCancellationToken))
            {
                throw new BusinessException("User has overdue borrowed books.");
            }

            var activeBorrowCount = await borrowRepository.CountActiveByUserIdAsync(
                userId,
                transactionCancellationToken);
            if (activeBorrowCount >= BorrowingLoanPolicy.MaxActiveBorrowCount)
            {
                throw new BusinessException("User has reached the maximum active borrow limit.");
            }

            if (await borrowRepository.GetActiveByUserIdAndBookIdAsync(
                    userId,
                    bookId,
                    transactionCancellationToken) is not null)
            {
                throw new BusinessException($"Book with id '{bookId}' is already borrowed by the current user.");
            }

            var bookInventory = await bookInventoryService.GetInventoryAsync(bookId, transactionCancellationToken);
            if (bookInventory is null)
            {
                throw new NotFoundException($"Book with id '{bookId}' was not found.");
            }

            if (bookInventory.Stock <= 0)
            {
                throw new BusinessException($"Book with id '{bookId}' is out of stock.");
            }

            var stock = await bookInventoryService.DecreaseStockAsync(bookId, transactionCancellationToken);

            var borrowedAt = clock.UtcNow;
            var borrowRecord = new BorrowRecord(Guid.NewGuid(), userId, bookId, borrowedAt);

            await borrowRepository.AddAsync(borrowRecord, transactionCancellationToken);
            await borrowRepository.SaveChangesAsync(transactionCancellationToken);

            return new BorrowingStockChangeResult(MapToResponseDto(borrowRecord, clock.UtcNow), bookId, stock);
        }, cancellationToken);

        await NotifyStockChangedAsync(result.BookId, result.Stock, cancellationToken);

        return result.BorrowRecord;
    }

    public async Task<BorrowRecordResponseDto> ReturnBookAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        var result = await transactionCoordinator.ExecuteAsync(async transactionCancellationToken =>
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
            var stock = await bookInventoryService.IncreaseStockAsync(bookId, transactionCancellationToken);
            await borrowRepository.SaveChangesAsync(transactionCancellationToken);

            return new BorrowingStockChangeResult(MapToResponseDto(borrowRecord, clock.UtcNow), bookId, stock);
        }, cancellationToken);

        await NotifyStockChangedAsync(result.BookId, result.Stock, cancellationToken);

        return result.BorrowRecord;
    }

    public async Task<BorrowRecordResponseDto> RenewBookAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var borrowRecord = await borrowRepository.GetActiveByUserIdAndBookIdAsync(
            userId,
            bookId,
            cancellationToken);

        if (borrowRecord is null)
        {
            throw new NotFoundException($"Active borrow record for book id '{bookId}' was not found.");
        }

        var utcNow = clock.UtcNow;
        if (borrowRecord.GetStatus(utcNow) == BorrowStatus.Overdue)
        {
            throw new BusinessException("Overdue borrow records cannot be renewed.");
        }

        if (borrowRecord.RenewalCount >= BorrowingLoanPolicy.MaxRenewalCount)
        {
            throw new BusinessException("The borrow record has already been renewed.");
        }

        borrowRecord.Renew(utcNow);
        await borrowRepository.UpdateAsync(borrowRecord, cancellationToken);
        await borrowRepository.SaveChangesAsync(cancellationToken);

        return MapToResponseDto(borrowRecord, clock.UtcNow);
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

    public async Task<PagedBorrowHistoryResponseDto> GetHistoryAsync(
        GetBorrowHistoryQueryDto query,
        CancellationToken cancellationToken = default)
    {
        await getBorrowHistoryQueryValidator.ValidateAndThrowAsync(query, cancellationToken);

        var userId = GetCurrentUserId();
        var page = await borrowRepository.GetPageByUserIdAsync(
            userId,
            query.Page,
            query.PageSize,
            cancellationToken);
        var bookIds = page.Items
            .Select(borrowRecord => borrowRecord.BookId)
            .Distinct()
            .ToArray();

        var books = await bookLookupService.GetByIdsAsync(bookIds, cancellationToken);
        var booksById = books.ToDictionary(book => book.Id);
        var totalPages = page.TotalCount == 0
            ? 0
            : (int)Math.Ceiling(page.TotalCount / (double)page.PageSize);

        return new PagedBorrowHistoryResponseDto(
            MapToResponseDtos(page.Items, booksById),
            page.Page,
            page.PageSize,
            page.TotalCount,
            totalPages);
    }

    public async Task<PagedOverdueBorrowRecordsResponseDto> GetOverdueAsync(
        GetOverdueBorrowRecordsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        await getOverdueBorrowRecordsQueryValidator.ValidateAndThrowAsync(query, cancellationToken);

        var utcNow = clock.UtcNow;
        var page = await borrowRepository.GetOverduePageAsync(
            query.Page,
            query.PageSize,
            utcNow,
            cancellationToken);

        var bookIds = page.Items
            .Select(borrowRecord => borrowRecord.BookId)
            .Distinct()
            .ToArray();
        var userIds = page.Items
            .Select(borrowRecord => borrowRecord.UserId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var books = await bookLookupService.GetByIdsAsync(bookIds, cancellationToken);
        var users = await userDirectory.GetByIdsAsync(userIds, cancellationToken);
        var booksById = books.ToDictionary(book => book.Id);
        var usersById = users.ToDictionary(user => user.Id, StringComparer.OrdinalIgnoreCase);
        var totalPages = page.TotalCount == 0
            ? 0
            : (int)Math.Ceiling(page.TotalCount / (double)page.PageSize);

        return new PagedOverdueBorrowRecordsResponseDto(
            page.Items
                .Select(borrowRecord => MapToOverdueResponseDto(
                    borrowRecord,
                    utcNow,
                    booksById.GetValueOrDefault(borrowRecord.BookId),
                    usersById.GetValueOrDefault(borrowRecord.UserId)))
                .ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount,
            totalPages);
    }

    private string GetCurrentUserId()
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            throw new AuthenticationFailedException("Authenticated user is required.");
        }

        return currentUser.UserId;
    }

    private async Task NotifyStockChangedAsync(
        Guid bookId,
        int stock,
        CancellationToken cancellationToken)
    {
        try
        {
            await bookStockChangeNotifier.NotifyAsync(bookId, stock, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Book stock change notification failed for book {BookId}.",
                bookId);
        }
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
            borrowRecord.GetStatus(utcNow).ToString(),
            borrowRecord.RenewalCount,
            borrowRecord.GetOverdueDays(utcNow));
    }

    private static OverdueBorrowRecordResponseDto MapToOverdueResponseDto(
        BorrowRecord borrowRecord,
        DateTime utcNow,
        BookLookupItem? book,
        UserDirectoryItem? user)
    {
        return new OverdueBorrowRecordResponseDto(
            borrowRecord.Id,
            borrowRecord.UserId,
            user?.Username ?? borrowRecord.UserId,
            borrowRecord.BookId,
            book?.Name,
            book?.Author,
            borrowRecord.BorrowedAt,
            borrowRecord.DueDate,
            borrowRecord.GetOverdueDays(utcNow),
            borrowRecord.RenewalCount,
            BorrowStatus.Overdue.ToString());
    }

    private sealed record BorrowingStockChangeResult(
        BorrowRecordResponseDto BorrowRecord,
        Guid BookId,
        int Stock);
}
