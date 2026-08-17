namespace LibrarySystem.Modules.Books.Application.Contracts;

public interface IBookInventoryService
{
    Task<BookInventoryItem?> GetInventoryAsync(
        Guid bookId,
        CancellationToken cancellationToken = default);

    Task DecreaseStockAsync(
        Guid bookId,
        CancellationToken cancellationToken = default);

    Task IncreaseStockAsync(
        Guid bookId,
        CancellationToken cancellationToken = default);
}
