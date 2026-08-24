using LibrarySystem.Modules.Books.Application.Interfaces;
using LibrarySystem.Modules.Books.Application.Models;
using LibrarySystem.Modules.Books.Domain;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Modules.Books.Infrastructure.Repositories;

internal sealed class BookRepository(BooksDbContext dbContext) : IBookRepository
{
    public async Task<BookPage> GetPageAsync(
        int page,
        int pageSize,
        string? search,
        string sortBy,
        string sortDirection,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Books.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = $"%{search}%";

            query = query.Where(book =>
                EF.Functions.ILike(book.Name, searchPattern) ||
                EF.Functions.ILike(book.Author, searchPattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await ApplySorting(query, sortBy, sortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new BookPage(items, page, pageSize, totalCount);
    }

    public async Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(book => book.Id == id, cancellationToken);
    }

    public async Task<Book?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Books
            .FirstOrDefaultAsync(book => book.Id == id, cancellationToken);
    }

    public async Task AddAsync(Book book, CancellationToken cancellationToken = default)
    {
        await dbContext.Books.AddAsync(book, cancellationToken);
    }

    public Task DeleteAsync(Book book, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dbContext.Books.Remove(book);

        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IOrderedQueryable<Book> ApplySorting(
        IQueryable<Book> query,
        string sortBy,
        string sortDirection)
    {
        var descending = sortDirection == "desc";

        return sortBy switch
        {
            "author" => descending
                ? query
                    .OrderByDescending(book => book.Author)
                    .ThenBy(book => book.Name)
                    .ThenBy(book => book.Id)
                : query
                    .OrderBy(book => book.Author)
                    .ThenBy(book => book.Name)
                    .ThenBy(book => book.Id),
            "stock" => descending
                ? query
                    .OrderByDescending(book => book.Stock)
                    .ThenBy(book => book.Name)
                    .ThenBy(book => book.Id)
                : query
                    .OrderBy(book => book.Stock)
                    .ThenBy(book => book.Name)
                    .ThenBy(book => book.Id),
            _ => descending
                ? query
                    .OrderByDescending(book => book.Name)
                    .ThenBy(book => book.Id)
                : query
                    .OrderBy(book => book.Name)
                    .ThenBy(book => book.Id)
        };
    }
}
