using System.Net;
using System.Net.Http.Json;
using LibrarySystem.Api.IntegrationTests.Infrastructure;
using LibrarySystem.Modules.Books.Domain;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Borrowing.Domain;
using LibrarySystem.Modules.Borrowing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibrarySystem.Api.IntegrationTests.Borrowing;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class BorrowingControllerTests(LibrarySystemApiFactory factory) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await factory.ResetDataAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task BorrowBook_WithAvailableStock_ReturnsSuccess()
    {
        using var client = factory.CreateApiClient();
        var bookId = await SeedBookAsync(stock: 2);

        var response = await client.PostAsync($"/api/borrow/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecord = await response.Content.ReadFromJsonAsync<BorrowRecordResponse>();

        Assert.NotNull(borrowRecord);
        Assert.Equal(bookId, borrowRecord.BookId);
        Assert.Null(borrowRecord.ReturnedAt);

        using var scope = factory.Services.CreateScope();
        var booksDbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var borrowingDbContext = scope.ServiceProvider.GetRequiredService<BorrowingDbContext>();

        var stock = await booksDbContext.Books
            .Where(book => book.Id == bookId)
            .Select(book => book.Stock)
            .SingleAsync();

        var storedBorrowRecord = await borrowingDbContext.BorrowRecords.SingleAsync();

        Assert.Equal(1, stock);
        Assert.Equal(TestAuthenticationHandler.UserId, storedBorrowRecord.UserId);
        Assert.Equal(bookId, storedBorrowRecord.BookId);
        Assert.Null(storedBorrowRecord.ReturnedAt);
    }

    [Fact]
    public async Task BorrowBook_TwiceWithoutReturn_ReturnsBadRequest()
    {
        using var client = factory.CreateApiClient();
        var bookId = await SeedBookAsync(stock: 2);

        var firstResponse = await client.PostAsync($"/api/borrow/{bookId}", content: null);
        var secondResponse = await client.PostAsync($"/api/borrow/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        Assert.Equal("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BorrowBook_WithZeroStock_ReturnsBadRequest()
    {
        using var client = factory.CreateApiClient();
        var bookId = await SeedBookAsync(stock: 0);

        var response = await client.PostAsync($"/api/borrow/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var booksDbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var borrowingDbContext = scope.ServiceProvider.GetRequiredService<BorrowingDbContext>();

        var stock = await booksDbContext.Books
            .Where(book => book.Id == bookId)
            .Select(book => book.Stock)
            .SingleAsync();
        var borrowRecordCount = await borrowingDbContext.BorrowRecords.CountAsync();

        Assert.Equal(0, stock);
        Assert.Equal(0, borrowRecordCount);
    }

    [Fact]
    public async Task GetMyBooks_ReturnsOnlyCurrentUsersActiveBorrows()
    {
        using var client = factory.CreateApiClient();
        const string currentUserBookName = "Current User Book";
        const string currentUserBookAuthor = "Current Author";
        var currentUserBookId = await SeedBookAsync(
            name: currentUserBookName,
            author: currentUserBookAuthor,
            stock: 2);
        var otherUserBookId = await SeedBookAsync(name: "Other User Book", stock: 2);
        var returnedBookId = await SeedBookAsync(name: "Returned Book", stock: 2);

        await SeedBorrowRecordAsync(TestAuthenticationHandler.UserId, currentUserBookId);
        await SeedBorrowRecordAsync("another-user-id", otherUserBookId);
        await SeedBorrowRecordAsync(TestAuthenticationHandler.UserId, returnedBookId, returnedAt: DateTime.UtcNow);

        var response = await client.GetAsync("/api/borrow/my-books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecords = await response.Content.ReadFromJsonAsync<List<BorrowRecordResponse>>();

        Assert.NotNull(borrowRecords);
        var borrowRecord = Assert.Single(borrowRecords);
        Assert.Equal(currentUserBookId, borrowRecord.BookId);
        Assert.Equal(currentUserBookName, borrowRecord.BookName);
        Assert.Equal(currentUserBookAuthor, borrowRecord.Author);
        Assert.Null(borrowRecord.ReturnedAt);
    }

    [Fact]
    public async Task GetMyBooks_WithMultipleBooks_ReturnsBookDetailsWithoutDuplicates()
    {
        using var client = factory.CreateApiClient();
        var firstBookId = await SeedBookAsync(
            name: "Domain-Driven Design",
            author: "Eric Evans",
            stock: 2);
        var secondBookId = await SeedBookAsync(
            name: "Patterns of Enterprise Application Architecture",
            author: "Martin Fowler",
            stock: 2);

        await SeedBorrowRecordAsync(TestAuthenticationHandler.UserId, firstBookId);
        await SeedBorrowRecordAsync(TestAuthenticationHandler.UserId, secondBookId);

        var response = await client.GetAsync("/api/borrow/my-books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecords = await response.Content.ReadFromJsonAsync<List<BorrowRecordResponse>>();

        Assert.NotNull(borrowRecords);
        Assert.Equal(2, borrowRecords.Count);

        var firstBorrowRecord = Assert.Single(borrowRecords, borrowRecord => borrowRecord.BookId == firstBookId);
        var secondBorrowRecord = Assert.Single(borrowRecords, borrowRecord => borrowRecord.BookId == secondBookId);

        Assert.Equal("Domain-Driven Design", firstBorrowRecord.BookName);
        Assert.Equal("Eric Evans", firstBorrowRecord.Author);
        Assert.Equal("Patterns of Enterprise Application Architecture", secondBorrowRecord.BookName);
        Assert.Equal("Martin Fowler", secondBorrowRecord.Author);
    }

    [Fact]
    public async Task ReturnBook_WithActiveBorrow_ReturnsSuccess()
    {
        using var client = factory.CreateApiClient();
        var bookId = await SeedBookAsync(stock: 1);
        await SeedBorrowRecordAsync(TestAuthenticationHandler.UserId, bookId);

        var response = await client.PostAsync($"/api/return/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecord = await response.Content.ReadFromJsonAsync<BorrowRecordResponse>();

        Assert.NotNull(borrowRecord);
        Assert.Equal(bookId, borrowRecord.BookId);
        Assert.NotNull(borrowRecord.ReturnedAt);

        using var scope = factory.Services.CreateScope();
        var booksDbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var borrowingDbContext = scope.ServiceProvider.GetRequiredService<BorrowingDbContext>();

        var stock = await booksDbContext.Books
            .Where(book => book.Id == bookId)
            .Select(book => book.Stock)
            .SingleAsync();

        var storedBorrowRecord = await borrowingDbContext.BorrowRecords.SingleAsync();

        Assert.Equal(2, stock);
        Assert.NotNull(storedBorrowRecord.ReturnedAt);
    }

    [Fact]
    public async Task ReturnBook_WithoutActiveBorrow_ReturnsExpectedError()
    {
        using var client = factory.CreateApiClient();
        var bookId = await SeedBookAsync(stock: 1);

        var response = await client.PostAsync($"/api/return/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BorrowEndpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        using var client = factory.CreateUnauthenticatedApiClient();
        var bookId = await SeedBookAsync(stock: 1);

        var response = await client.PostAsync($"/api/borrow/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Guid> SeedBookAsync(
        string name = "Clean Code",
        string author = "Robert C. Martin",
        int stock = 1)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var book = new Book(Guid.NewGuid(), name, author, stock);

        await dbContext.Books.AddAsync(book);
        await dbContext.SaveChangesAsync();

        return book.Id;
    }

    private async Task SeedBorrowRecordAsync(
        string userId,
        Guid bookId,
        DateTime? returnedAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BorrowingDbContext>();
        var borrowRecord = new BorrowRecord(Guid.NewGuid(), userId, bookId, DateTime.UtcNow.AddMinutes(-5));

        if (returnedAt is not null)
        {
            borrowRecord.Return(returnedAt.Value);
        }

        await dbContext.BorrowRecords.AddAsync(borrowRecord);
        await dbContext.SaveChangesAsync();
    }

    private sealed record BorrowRecordResponse(
        Guid Id,
        Guid BookId,
        string? BookName,
        string? Author,
        DateTime BorrowedAt,
        DateTime? ReturnedAt);
}
