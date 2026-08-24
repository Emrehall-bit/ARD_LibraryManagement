using System.Net;
using System.Net.Http.Json;
using LibrarySystem.Api.IntegrationTests.Infrastructure;
using LibrarySystem.Modules.Books.Domain;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Borrowing.Domain;
using LibrarySystem.Modules.Borrowing.Infrastructure;
using LibrarySystem.Shared.Authentication;
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
        var beforeBorrow = DateTime.UtcNow;

        var response = await client.PostAsync($"/api/borrow/{bookId}", content: null);
        var afterBorrow = DateTime.UtcNow;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecord = await response.Content.ReadFromJsonAsync<BorrowRecordResponse>();

        Assert.NotNull(borrowRecord);
        Assert.Equal(bookId, borrowRecord.BookId);
        Assert.InRange(borrowRecord.BorrowedAt, beforeBorrow.AddSeconds(-1), afterBorrow.AddSeconds(1));
        Assert.Equal(
            TimeSpan.FromDays(BorrowingLoanPolicy.DefaultLoanPeriodDays),
            borrowRecord.DueDate - borrowRecord.BorrowedAt);
        Assert.Null(borrowRecord.ReturnedAt);
        Assert.Equal(nameof(BorrowStatus.Borrowed), borrowRecord.Status);

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
        Assert.Equal(
            TimeSpan.FromDays(BorrowingLoanPolicy.DefaultLoanPeriodDays),
            storedBorrowRecord.DueDate - storedBorrowRecord.BorrowedAt);
        Assert.Null(storedBorrowRecord.ReturnedAt);
    }

    [Theory]
    [InlineData(IdentityRoles.Member)]
    [InlineData(IdentityRoles.Admin)]
    public async Task BorrowBook_WithAuthenticatedRole_ReturnsSuccess(string role)
    {
        using var client = CreateAuthenticatedClient(role, $"borrow-{role.ToLowerInvariant()}-user");
        var bookId = await SeedBookAsync(stock: 1);

        var response = await client.PostAsync($"/api/borrow/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
        Assert.NotEqual(default, borrowRecord.DueDate);
        Assert.Null(borrowRecord.ReturnedAt);
        Assert.Equal(nameof(BorrowStatus.Borrowed), borrowRecord.Status);
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
    public async Task GetMyBooks_WithPastDueActiveBorrow_ReturnsOverdueStatus()
    {
        using var client = factory.CreateApiClient();
        var borrowedAt = DateTime.UtcNow.AddDays(-20);
        var bookId = await SeedBookAsync(name: "Overdue Book", stock: 2);

        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            bookId,
            borrowedAt: borrowedAt,
            dueDate: borrowedAt.AddDays(BorrowingLoanPolicy.DefaultLoanPeriodDays));

        var response = await client.GetAsync("/api/borrow/my-books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecords = await response.Content.ReadFromJsonAsync<List<BorrowRecordResponse>>();

        Assert.NotNull(borrowRecords);
        var borrowRecord = Assert.Single(borrowRecords);
        Assert.Equal(bookId, borrowRecord.BookId);
        Assert.Equal(nameof(BorrowStatus.Overdue), borrowRecord.Status);
        Assert.Null(borrowRecord.ReturnedAt);
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
        Assert.NotEqual(default, borrowRecord.DueDate);
        Assert.NotNull(borrowRecord.ReturnedAt);
        Assert.Equal(nameof(BorrowStatus.Returned), borrowRecord.Status);

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
    public async Task ReturnBook_WithOverdueBorrow_ReturnsSuccess()
    {
        using var client = factory.CreateApiClient();
        var borrowedAt = DateTime.UtcNow.AddDays(-20);
        var bookId = await SeedBookAsync(name: "Return Overdue Book", stock: 1);

        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            bookId,
            borrowedAt: borrowedAt,
            dueDate: borrowedAt.AddDays(BorrowingLoanPolicy.DefaultLoanPeriodDays));

        var response = await client.PostAsync($"/api/return/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecord = await response.Content.ReadFromJsonAsync<BorrowRecordResponse>();

        Assert.NotNull(borrowRecord);
        Assert.NotNull(borrowRecord.ReturnedAt);
        Assert.Equal(nameof(BorrowStatus.Returned), borrowRecord.Status);
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

    [Fact]
    public async Task GetHistory_ReturnsActiveAndReturnedBorrowRecords()
    {
        using var client = factory.CreateApiClient();
        var activeBookId = await SeedBookAsync(name: "History Active Book", stock: 2);
        var returnedBookId = await SeedBookAsync(name: "History Returned Book", stock: 2);
        var returnedBorrowedAt = DateTime.UtcNow.AddDays(-3);

        await SeedBorrowRecordAsync(TestAuthenticationHandler.UserId, activeBookId);
        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            returnedBookId,
            borrowedAt: returnedBorrowedAt,
            returnedAt: returnedBorrowedAt.AddDays(1));

        var response = await client.GetAsync("/api/borrow/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecords = await response.Content.ReadFromJsonAsync<List<BorrowRecordResponse>>();

        Assert.NotNull(borrowRecords);
        Assert.Equal(2, borrowRecords.Count);
        Assert.Contains(borrowRecords, borrowRecord =>
            borrowRecord.BookId == activeBookId &&
            borrowRecord.Status == nameof(BorrowStatus.Borrowed));
        Assert.Contains(borrowRecords, borrowRecord =>
            borrowRecord.BookId == returnedBookId &&
            borrowRecord.Status == nameof(BorrowStatus.Returned) &&
            borrowRecord.ReturnedAt is not null);
    }

    [Fact]
    public async Task GetHistory_ReturnsOnlyCurrentUsersBorrowRecords()
    {
        using var client = factory.CreateApiClient();
        var currentUserBookId = await SeedBookAsync(name: "Current User History Book", stock: 2);
        var otherUserBookId = await SeedBookAsync(name: "Other User History Book", stock: 2);

        await SeedBorrowRecordAsync(TestAuthenticationHandler.UserId, currentUserBookId);
        await SeedBorrowRecordAsync("history-other-user", otherUserBookId);

        var response = await client.GetAsync("/api/borrow/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecords = await response.Content.ReadFromJsonAsync<List<BorrowRecordResponse>>();

        Assert.NotNull(borrowRecords);
        var borrowRecord = Assert.Single(borrowRecords);
        Assert.Equal(currentUserBookId, borrowRecord.BookId);
    }

    [Fact]
    public async Task GetHistory_ReturnsBorrowRecordsNewestFirst()
    {
        using var client = factory.CreateApiClient();
        var olderBookId = await SeedBookAsync(name: "Older History Book", stock: 2);
        var newerBookId = await SeedBookAsync(name: "Newer History Book", stock: 2);
        var olderBorrowedAt = DateTime.UtcNow.AddDays(-5);
        var newerBorrowedAt = DateTime.UtcNow.AddDays(-1);

        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            olderBookId,
            borrowedAt: olderBorrowedAt);
        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            newerBookId,
            borrowedAt: newerBorrowedAt);

        var response = await client.GetAsync("/api/borrow/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecords = await response.Content.ReadFromJsonAsync<List<BorrowRecordResponse>>();

        Assert.NotNull(borrowRecords);
        Assert.Equal([newerBookId, olderBookId], borrowRecords.Select(borrowRecord => borrowRecord.BookId));
    }

    [Fact]
    public async Task GetMyBooks_DoesNotReturnReturnedBorrowRecords()
    {
        using var client = factory.CreateApiClient();
        var activeBookId = await SeedBookAsync(name: "My Active Book", stock: 2);
        var returnedBookId = await SeedBookAsync(name: "My Returned Book", stock: 2);
        var returnedBorrowedAt = DateTime.UtcNow.AddDays(-2);

        await SeedBorrowRecordAsync(TestAuthenticationHandler.UserId, activeBookId);
        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            returnedBookId,
            borrowedAt: returnedBorrowedAt,
            returnedAt: returnedBorrowedAt.AddDays(1));

        var response = await client.GetAsync("/api/borrow/my-books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecords = await response.Content.ReadFromJsonAsync<List<BorrowRecordResponse>>();

        Assert.NotNull(borrowRecords);
        var borrowRecord = Assert.Single(borrowRecords);
        Assert.Equal(activeBookId, borrowRecord.BookId);
    }

    [Fact]
    public async Task GetHistory_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = factory.CreateUnauthenticatedApiClient();

        var response = await client.GetAsync("/api/borrow/history");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(IdentityRoles.Member)]
    [InlineData(IdentityRoles.Admin)]
    public async Task GetHistory_WithAuthenticatedRole_ReturnsSuccess(string role)
    {
        using var client = CreateAuthenticatedClient(role, $"history-{role.ToLowerInvariant()}-user");

        var response = await client.GetAsync("/api/borrow/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    private async Task<Guid> SeedBorrowRecordAsync(
        string userId,
        Guid bookId,
        DateTime? borrowedAt = null,
        DateTime? dueDate = null,
        DateTime? returnedAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BorrowingDbContext>();
        var resolvedBorrowedAt = borrowedAt ?? DateTime.UtcNow.AddMinutes(-5);
        var borrowRecord = new BorrowRecord(
            Guid.NewGuid(),
            userId,
            bookId,
            resolvedBorrowedAt,
            dueDate ?? resolvedBorrowedAt.AddDays(BorrowingLoanPolicy.DefaultLoanPeriodDays));

        if (returnedAt is not null)
        {
            borrowRecord.Return(returnedAt.Value);
        }

        await dbContext.BorrowRecords.AddAsync(borrowRecord);
        await dbContext.SaveChangesAsync();

        return borrowRecord.Id;
    }

    private HttpClient CreateAuthenticatedClient(string role, string userId)
    {
        var client = factory.CreateUnauthenticatedApiClient();

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.HeaderName, userId);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, userId);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeaderName, role);

        return client;
    }

    private sealed record BorrowRecordResponse(
        Guid Id,
        Guid BookId,
        string? BookName,
        string? Author,
        DateTime BorrowedAt,
        DateTime DueDate,
        DateTime? ReturnedAt,
        string Status);
}
