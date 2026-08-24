using LibrarySystem.Modules.Books.Application.Contracts;
using LibrarySystem.Modules.Identity.Application.Contracts;
using LibrarySystem.Shared.Borrowing;

namespace LibrarySystem.Api.AdminDashboard;

internal sealed class AdminDashboardService(
    IUserDirectory userDirectory,
    IBookCatalogSummaryService bookCatalogSummaryService,
    IBookLookupService bookLookupService,
    IAdminBorrowingDashboardSummaryService borrowingDashboardSummaryService) : IAdminDashboardService
{
    public async Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var totalUsersTask = userDirectory.CountAsync(cancellationToken);
        var bookSummaryTask = bookCatalogSummaryService.GetSummaryAsync(cancellationToken);
        var borrowingSummaryTask = borrowingDashboardSummaryService.GetSummaryAsync(cancellationToken);

        await Task.WhenAll(totalUsersTask, bookSummaryTask, borrowingSummaryTask);

        var bookSummary = await bookSummaryTask;
        var borrowingSummary = await borrowingSummaryTask;
        var userIds = borrowingSummary.RecentOverdueBorrows
            .Select(borrowRecord => borrowRecord.UserId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var bookIds = borrowingSummary.RecentOverdueBorrows
            .Select(borrowRecord => borrowRecord.BookId)
            .Distinct()
            .ToArray();
        var users = await userDirectory.GetByIdsAsync(userIds, cancellationToken);
        var books = await bookLookupService.GetByIdsAsync(bookIds, cancellationToken);
        var usersById = users.ToDictionary(user => user.Id, StringComparer.OrdinalIgnoreCase);
        var booksById = books.ToDictionary(book => book.Id);

        return new AdminDashboardResponse(
            await totalUsersTask,
            bookSummary.TotalBooks,
            bookSummary.TotalStock,
            bookSummary.OutOfStockBooks,
            borrowingSummary.ActiveBorrows,
            borrowingSummary.OverdueBorrows,
            borrowingSummary.ReturnedBorrows,
            borrowingSummary.RecentOverdueBorrows
                .Select(borrowRecord =>
                {
                    usersById.TryGetValue(borrowRecord.UserId, out var user);
                    booksById.TryGetValue(borrowRecord.BookId, out var book);

                    return new RecentOverdueBorrowResponse(
                        borrowRecord.Id,
                        borrowRecord.UserId,
                        user?.Username ?? borrowRecord.UserId,
                        borrowRecord.BookId,
                        book?.Name,
                        book?.Author,
                        borrowRecord.DueDate,
                        borrowRecord.OverdueDays);
                })
                .ToList());
    }
}
