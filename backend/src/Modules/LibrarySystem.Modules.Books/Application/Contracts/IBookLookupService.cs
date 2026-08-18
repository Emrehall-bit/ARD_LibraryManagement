namespace LibrarySystem.Modules.Books.Application.Contracts;

public interface IBookLookupService
{
    Task<IReadOnlyList<BookLookupItem>> GetByIdsAsync(
        IEnumerable<Guid> bookIds,
        CancellationToken cancellationToken = default);
}
