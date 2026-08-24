using LibrarySystem.Modules.Borrowing.Application.Interfaces;

namespace LibrarySystem.Api.IntegrationTests.Infrastructure;

public sealed class TestBookStockChangeNotifier : IBookStockChangeNotifier
{
    private readonly object syncRoot = new();
    private readonly List<BookStockChangeNotification> notifications = [];

    public bool ThrowOnNotify { get; set; }

    public IReadOnlyList<BookStockChangeNotification> Notifications
    {
        get
        {
            lock (syncRoot)
            {
                return notifications.ToList();
            }
        }
    }

    public Task NotifyAsync(
        Guid bookId,
        int stock,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnNotify)
        {
            throw new InvalidOperationException("Test stock notification failure.");
        }

        lock (syncRoot)
        {
            notifications.Add(new BookStockChangeNotification(bookId, stock));
        }

        return Task.CompletedTask;
    }

    public void Reset()
    {
        lock (syncRoot)
        {
            notifications.Clear();
            ThrowOnNotify = false;
        }
    }
}

public sealed record BookStockChangeNotification(Guid BookId, int Stock);
