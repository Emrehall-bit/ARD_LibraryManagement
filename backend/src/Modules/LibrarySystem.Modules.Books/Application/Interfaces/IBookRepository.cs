using LibrarySystem.Modules.Books.Domain;
using LibrarySystem.Modules.Books.Application.Models;

namespace LibrarySystem.Modules.Books.Application.Interfaces;

public interface IBookRepository
{
    Task<BookPage> GetPageAsync(
        int page,
        int pageSize,
        string? search,
        string sortBy,
        string sortDirection,
        string stockStatus,
        BookCategory? category,
        CancellationToken cancellationToken = default);

    Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Book?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Book?> GetTrackedByIdWithImagesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetCoverObjectNamesByBookIdsAsync(
        IReadOnlyCollection<Guid> bookIds,
        CancellationToken cancellationToken = default);

    Task<int> CountImagesByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default);

    Task<BookImage?> GetImageByIdAndBookIdAsync(
        Guid bookId,
        Guid imageId,
        CancellationToken cancellationToken = default);

    Task AddImageAsync(
        BookImage image,
        bool makeCover,
        CancellationToken cancellationToken = default);

    Task<bool> SetCoverAsync(
        Guid bookId,
        Guid imageId,
        CancellationToken cancellationToken = default);

    Task<BookImage?> DeleteImageAsync(
        Guid bookId,
        Guid imageId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Book book, CancellationToken cancellationToken = default);

    Task DeleteAsync(Book book, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
