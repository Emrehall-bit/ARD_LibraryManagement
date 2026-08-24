using LibrarySystem.Modules.Borrowing.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace LibrarySystem.Api.Hubs;

internal sealed class SignalRBookStockChangeNotifier(
    IHubContext<LibraryHub> hubContext) : IBookStockChangeNotifier
{
    public const string EventName = "BookStockChanged";

    public async Task NotifyAsync(
        Guid bookId,
        int stock,
        CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.All.SendAsync(
            EventName,
            new BookStockChangedMessage(bookId, stock),
            cancellationToken);
    }
}
