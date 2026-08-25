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
        string stockStatus,
        BookCategory? category,
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

        query = ApplyStockFilter(query, stockStatus);
        query = ApplyCategoryFilter(query, category);

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
            .Include(book => book.Images)
            .FirstOrDefaultAsync(book => book.Id == id, cancellationToken);
    }

    public async Task<Book?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Books
            .FirstOrDefaultAsync(book => book.Id == id, cancellationToken);
    }

    public async Task<Book?> GetTrackedByIdWithImagesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Books
            .Include(book => book.Images)
            .FirstOrDefaultAsync(book => book.Id == id, cancellationToken);
    }

    public async Task<int> CountImagesByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        return await dbContext.BookImages
            .AsNoTracking()
            .CountAsync(image => image.BookId == bookId, cancellationToken);
    }

    public async Task<BookImage?> GetImageByIdAndBookIdAsync(
        Guid bookId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BookImages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                image =>
                    image.BookId == bookId &&
                    image.Id == imageId,
                cancellationToken);
    }

    public async Task AddImageAsync(
        BookImage image,
        bool makeCover,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (makeCover)
        {
            await dbContext.BookImages
                .Where(existingImage =>
                    existingImage.BookId == image.BookId &&
                    existingImage.IsCover)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(existingImage => existingImage.IsCover, false),
                    cancellationToken);
        }

        await dbContext.BookImages.AddAsync(image, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> SetCoverAsync(
        Guid bookId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var selectedImage = await dbContext.BookImages
            .FirstOrDefaultAsync(
                image =>
                    image.BookId == bookId &&
                    image.Id == imageId,
                cancellationToken);

        if (selectedImage is null)
        {
            return false;
        }

        if (selectedImage.IsCover)
        {
            return true;
        }

        await dbContext.BookImages
            .Where(image =>
                image.BookId == bookId &&
                image.IsCover)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(image => image.IsCover, false),
                cancellationToken);

        selectedImage.SetCover(true);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    public async Task<BookImage?> DeleteImageAsync(
        Guid bookId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var images = await dbContext.BookImages
            .Where(image => image.BookId == bookId)
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.Id)
            .ToListAsync(cancellationToken);
        var image = images.FirstOrDefault(image => image.Id == imageId);

        if (image is null)
        {
            return null;
        }

        dbContext.BookImages.Remove(image);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (image.IsCover)
        {
            var nextCover = images.FirstOrDefault(nextImage => nextImage.Id != imageId);
            if (nextCover is not null)
            {
                nextCover.SetCover(true);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return image;
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

    private static IQueryable<Book> ApplyStockFilter(IQueryable<Book> query, string stockStatus)
    {
        return stockStatus switch
        {
            "instock" => query.Where(book => book.Stock > 0),
            "outofstock" => query.Where(book => book.Stock == 0),
            _ => query
        };
    }

    private static IQueryable<Book> ApplyCategoryFilter(IQueryable<Book> query, BookCategory? category)
    {
        return category is null
            ? query
            : query.Where(book => book.Category == category);
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
