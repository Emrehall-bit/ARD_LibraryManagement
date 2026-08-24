namespace LibrarySystem.Modules.Books.Application.Contracts;

public interface IBookCatalogSummaryService
{
    Task<BookCatalogSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
