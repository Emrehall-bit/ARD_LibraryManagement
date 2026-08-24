using LibrarySystem.Modules.Books.Application.Contracts;
using LibrarySystem.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Modules.Books.Infrastructure.Services;

internal sealed class BookInventoryService(BooksDbContext dbContext) : IBookInventoryService
{
    public async Task<BookInventoryItem?> GetInventoryAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Books
            .AsNoTracking()
            .Where(book => book.Id == bookId)
            .Select(book => new BookInventoryItem(book.Id, book.Stock))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> DecreaseStockAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        var book = await GetBookOrThrowAsync(bookId, cancellationToken);

        if (book.Stock <= 0)
        {
            throw new BusinessException($"Book with id '{bookId}' is out of stock.");
        }

        book.DecreaseStock();

        await dbContext.SaveChangesAsync(cancellationToken);

        return book.Stock;
    }

    public async Task<int> IncreaseStockAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        var book = await GetBookOrThrowAsync(bookId, cancellationToken);

        book.IncreaseStock();

        await dbContext.SaveChangesAsync(cancellationToken);

        return book.Stock;
    }

    private async Task<Domain.Book> GetBookOrThrowAsync(Guid bookId, CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .FirstOrDefaultAsync(book => book.Id == bookId, cancellationToken);

        return book ?? throw new NotFoundException($"Book with id '{bookId}' was not found.");
    }
}
