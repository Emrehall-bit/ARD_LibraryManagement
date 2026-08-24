using LibrarySystem.Modules.Books.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Modules.Books.Infrastructure.Services;

internal sealed class BookCatalogSummaryService(BooksDbContext dbContext) : IBookCatalogSummaryService
{
    public async Task<BookCatalogSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var totalBooks = await dbContext.Books
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var totalStock = await dbContext.Books
            .AsNoTracking()
            .SumAsync(book => (int?)book.Stock, cancellationToken) ?? 0;
        var outOfStockBooks = await dbContext.Books
            .AsNoTracking()
            .CountAsync(book => book.Stock == 0, cancellationToken);

        return new BookCatalogSummary(totalBooks, totalStock, outOfStockBooks);
    }
}
