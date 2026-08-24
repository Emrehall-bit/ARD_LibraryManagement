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
        CancellationToken cancellationToken = default);

    Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Book?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Book book, CancellationToken cancellationToken = default);

    Task DeleteAsync(Book book, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
