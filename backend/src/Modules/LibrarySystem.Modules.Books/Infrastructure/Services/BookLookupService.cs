using LibrarySystem.Modules.Books.Application.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Modules.Books.Infrastructure.Services;

internal sealed class BookLookupService(BooksDbContext dbContext) : IBookLookupService
{
    public async Task<IReadOnlyList<BookLookupItem>> GetByIdsAsync(
        IEnumerable<Guid> bookIds,
        CancellationToken cancellationToken = default)
    {
        var distinctBookIds = bookIds
            .Distinct()
            .ToArray();

        if (distinctBookIds.Length == 0)
        {
            return [];
        }

        return await dbContext.Books
            .AsNoTracking()
            .Where(book => distinctBookIds.Contains(book.Id))
            .Select(book => new BookLookupItem(
                book.Id,
                book.Name,
                book.Author))
            .ToListAsync(cancellationToken);
    }
}
