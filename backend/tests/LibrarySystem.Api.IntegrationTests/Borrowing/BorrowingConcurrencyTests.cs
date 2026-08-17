using System.Net;
using LibrarySystem.Api.IntegrationTests.Infrastructure;
using LibrarySystem.Modules.Books.Domain;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Borrowing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LibrarySystem.Api.IntegrationTests.Borrowing;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class BorrowingConcurrencyTests(LibrarySystemApiFactory factory) : IAsyncLifetime
{
    private static readonly HashSet<HttpStatusCode> ExpectedConcurrencyFailureStatusCodes =
    [
        HttpStatusCode.BadRequest,
        HttpStatusCode.Conflict
    ];

    public async Task InitializeAsync()
    {
        await factory.ResetDataAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task BorrowBook_ConcurrentUsers_WithSingleStock_AllowsOnlyOneBorrow()
    {
        const int initialStock = 1;
        var bookId = await SeedBookAsync(stock: initialStock);
        using var firstClient = CreateAuthenticatedClient("concurrent-user-1");
        using var secondClient = CreateAuthenticatedClient("concurrent-user-2");

        var responses = await PostBorrowConcurrentlyWhileBookRowIsLockedAsync(
            bookId,
            firstClient,
            secondClient);

        var successfulResponses = responses.Count(response => response.IsSuccessStatusCode);
        var failedResponses = responses.Count(response => !response.IsSuccessStatusCode);

        Assert.Equal(1, successfulResponses);
        Assert.Equal(1, failedResponses);

        var failedResponse = Assert.Single(responses, response => !response.IsSuccessStatusCode);

        Assert.Equal(HttpStatusCode.Conflict, failedResponse.StatusCode);
        Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);

        var state = await GetBorrowingStateAsync(bookId);

        Assert.Equal(0, state.FinalStock);
        Assert.True(state.FinalStock >= 0);
        Assert.Equal(1, state.ActiveBorrowCount);
        Assert.Equal(1, state.TotalBorrowCount);
        Assert.Equal(initialStock, state.FinalStock + state.ActiveBorrowCount);
    }

    [Fact]
    public async Task BorrowBook_ConcurrentRequests_SameUserSameBook_CreatesSingleActiveBorrow()
    {
        const int initialStock = 2;
        var bookId = await SeedBookAsync(stock: initialStock);
        using var firstClient = CreateAuthenticatedClient("same-user");
        using var secondClient = CreateAuthenticatedClient("same-user");

        var responses = await PostBorrowConcurrentlyAsync(bookId, firstClient, secondClient);

        var successfulResponses = responses.Count(response => response.IsSuccessStatusCode);
        var failedResponses = responses.Count(response => !response.IsSuccessStatusCode);

        Assert.Equal(1, successfulResponses);
        Assert.Equal(1, failedResponses);
        Assert.All(
            responses.Where(response => !response.IsSuccessStatusCode),
            response => Assert.Contains(response.StatusCode, ExpectedConcurrencyFailureStatusCodes));
        Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);

        var state = await GetBorrowingStateAsync(bookId);

        Assert.Equal(1, state.FinalStock);
        Assert.True(state.FinalStock >= 0);
        Assert.Equal(1, state.ActiveBorrowCount);
        Assert.Equal(1, state.TotalBorrowCount);
        Assert.Equal(initialStock, state.FinalStock + state.ActiveBorrowCount);
    }

    private HttpClient CreateAuthenticatedClient(string userId)
    {
        var client = factory.CreateUnauthenticatedApiClient();

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.HeaderName, userId);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, userId);

        return client;
    }

    private static async Task<IReadOnlyList<HttpResponseMessage>> PostBorrowConcurrentlyAsync(
        Guid bookId,
        params HttpClient[] clients)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = clients
            .Select(client => Task.Run(async () =>
            {
                await start.Task;

                return await client.PostAsync($"/api/borrow/{bookId}", content: null);
            }))
            .ToArray();

        start.SetResult();

        return await Task.WhenAll(tasks);
    }

    private async Task<IReadOnlyList<HttpResponseMessage>> PostBorrowConcurrentlyWhileBookRowIsLockedAsync(
        Guid bookId,
        params HttpClient[] clients)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "SELECT id FROM books.books WHERE id = @bookId FOR UPDATE;";
        command.Parameters.AddWithValue("bookId", bookId);

        await command.ExecuteNonQueryAsync();

        var responsesTask = PostBorrowConcurrentlyAsync(bookId, clients);

        await Task.Delay(TimeSpan.FromMilliseconds(500));
        await transaction.CommitAsync();

        return await responsesTask;
    }

    private async Task<Guid> SeedBookAsync(int stock)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var book = new Book(Guid.NewGuid(), $"Concurrent Book {Guid.NewGuid():N}", "Integration Test", stock);

        await dbContext.Books.AddAsync(book);
        await dbContext.SaveChangesAsync();

        return book.Id;
    }

    private string GetConnectionString()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();

        return dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Books test database connection string was not configured.");
    }

    private async Task<BorrowingState> GetBorrowingStateAsync(Guid bookId)
    {
        using var scope = factory.Services.CreateScope();
        var booksDbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var borrowingDbContext = scope.ServiceProvider.GetRequiredService<BorrowingDbContext>();

        var finalStock = await booksDbContext.Books
            .AsNoTracking()
            .Where(book => book.Id == bookId)
            .Select(book => book.Stock)
            .SingleAsync();

        var totalBorrowCount = await borrowingDbContext.BorrowRecords
            .AsNoTracking()
            .CountAsync(borrowRecord => borrowRecord.BookId == bookId);

        var activeBorrowCount = await borrowingDbContext.BorrowRecords
            .AsNoTracking()
            .CountAsync(borrowRecord => borrowRecord.BookId == bookId && borrowRecord.ReturnedAt == null);

        return new BorrowingState(finalStock, totalBorrowCount, activeBorrowCount);
    }

    private sealed record BorrowingState(
        int FinalStock,
        int TotalBorrowCount,
        int ActiveBorrowCount);
}
