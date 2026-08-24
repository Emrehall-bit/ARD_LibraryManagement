namespace LibrarySystem.Modules.Books.Application.Contracts;

public interface IBookInventoryService
{
    Task<BookInventoryItem?> GetInventoryAsync(
        Guid bookId,
        CancellationToken cancellationToken = default);

    Task<int> DecreaseStockAsync(
        Guid bookId,
        CancellationToken cancellationToken = default);

    Task<int> IncreaseStockAsync(
        Guid bookId,
        CancellationToken cancellationToken = default);
}
