namespace LibrarySystem.Modules.Borrowing.Application.Interfaces;

public interface IBookStockChangeNotifier
{
    Task NotifyAsync(
        Guid bookId,
        int stock,
        CancellationToken cancellationToken = default);
}
