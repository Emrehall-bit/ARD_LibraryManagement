using System.Net;
using System.Net.Http.Json;
using LibrarySystem.Api.IntegrationTests.Infrastructure;
using LibrarySystem.Modules.Books.Domain;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Borrowing.Domain;
using LibrarySystem.Modules.Borrowing.Infrastructure;
using LibrarySystem.Modules.Identity.Domain;
using LibrarySystem.Modules.Identity.Infrastructure;
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
        Assert.Equal(0, borrowRecord.RenewalCount);
        Assert.Equal(0, borrowRecord.OverdueDays);

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
        Assert.Equal(0, storedBorrowRecord.RenewalCount);

        var notification = Assert.Single(factory.BookStockChangeNotifications.Notifications);
        Assert.Equal(bookId, notification.BookId);
        Assert.Equal(1, notification.Stock);
    }

    [Fact]
    public async Task BorrowBook_WhenStockNotificationFails_ReturnsSuccessAndKeepsCommittedChanges()
    {
        using var client = factory.CreateApiClient();
        var bookId = await SeedBookAsync(stock: 2);
        factory.BookStockChangeNotifications.ThrowOnNotify = true;

        try
        {
            var response = await client.PostAsync($"/api/borrow/{bookId}", content: null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            factory.BookStockChangeNotifications.ThrowOnNotify = false;
        }

        using var scope = factory.Services.CreateScope();
        var booksDbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var borrowingDbContext = scope.ServiceProvider.GetRequiredService<BorrowingDbContext>();

        var stock = await booksDbContext.Books
            .Where(book => book.Id == bookId)
            .Select(book => book.Stock)
            .SingleAsync();
        var borrowRecordCount = await borrowingDbContext.BorrowRecords.CountAsync();

        Assert.Equal(1, stock);
        Assert.Equal(1, borrowRecordCount);
        Assert.Empty(factory.BookStockChangeNotifications.Notifications);
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
        Assert.Empty(factory.BookStockChangeNotifications.Notifications);
    }

    [Fact]
    public async Task BorrowBook_WithActiveNonOverdueBorrow_AllowsAnotherBorrow()
    {
        using var client = factory.CreateApiClient();
        var activeBookId = await SeedBookAsync(name: "Active Current Book", stock: 2);
        var nextBookId = await SeedBookAsync(name: "Allowed Next Book", stock: 2);

        await SeedBorrowRecordAsync(TestAuthenticationHandler.UserId, activeBookId);

        var response = await client.PostAsync($"/api/borrow/{nextBookId}", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var booksDbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var nextBookStock = await booksDbContext.Books
            .Where(book => book.Id == nextBookId)
            .Select(book => book.Stock)
            .SingleAsync();

        Assert.Equal(1, nextBookStock);
    }

    [Fact]
    public async Task BorrowBook_WithOverdueBorrow_ReturnsBadRequestWithoutChangingStockOrPublishingNotification()
    {
        using var client = factory.CreateApiClient();
        var overdueBookId = await SeedBookAsync(name: "Overdue Blocking Book", stock: 2);
        var targetBookId = await SeedBookAsync(name: "Blocked Target Book", stock: 3);
        var dueDate = DateTime.UtcNow.Date.AddDays(-2);

        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            overdueBookId,
            borrowedAt: dueDate.AddDays(-BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: dueDate);

        var response = await client.PostAsync($"/api/borrow/{targetBookId}", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>();

        Assert.Equal("Business rule violation.", problemDetails?.Title);
        Assert.Equal("User has overdue borrowed books.", problemDetails?.Detail);

        using var scope = factory.Services.CreateScope();
        var booksDbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var borrowingDbContext = scope.ServiceProvider.GetRequiredService<BorrowingDbContext>();

        var targetBookStock = await booksDbContext.Books
            .Where(book => book.Id == targetBookId)
            .Select(book => book.Stock)
            .SingleAsync();
        var targetBorrowCount = await borrowingDbContext.BorrowRecords
            .CountAsync(borrowRecord => borrowRecord.BookId == targetBookId);

        Assert.Equal(3, targetBookStock);
        Assert.Equal(0, targetBorrowCount);
        Assert.Empty(factory.BookStockChangeNotifications.Notifications);
    }

    [Fact]
    public async Task BorrowBook_WithAnotherUsersOverdueBorrow_ReturnsSuccess()
    {
        const string currentUserId = "not-overdue-user";
        using var client = CreateAuthenticatedClient(IdentityRoles.Member, currentUserId);
        var overdueBookId = await SeedBookAsync(name: "Other User Overdue Book", stock: 2);
        var targetBookId = await SeedBookAsync(name: "Other User Does Not Block Book", stock: 2);
        var dueDate = DateTime.UtcNow.Date.AddDays(-1);

        await SeedBorrowRecordAsync(
            "overdue-other-user",
            overdueBookId,
            borrowedAt: dueDate.AddDays(-BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: dueDate);

        var response = await client.PostAsync($"/api/borrow/{targetBookId}", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
        Assert.True(borrowRecord.OverdueDays > 0);
        Assert.Null(borrowRecord.ReturnedAt);
    }

    [Fact]
    public async Task GetMyBooks_WithBorrowedStatus_ReturnsZeroOverdueDays()
    {
        using var client = factory.CreateApiClient();
        var bookId = await SeedBookAsync(name: "Current Borrowed Book", stock: 2);

        await SeedBorrowRecordAsync(TestAuthenticationHandler.UserId, bookId);

        var response = await client.GetAsync("/api/borrow/my-books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecords = await response.Content.ReadFromJsonAsync<List<BorrowRecordResponse>>();

        Assert.NotNull(borrowRecords);
        var borrowRecord = Assert.Single(borrowRecords);
        Assert.Equal(nameof(BorrowStatus.Borrowed), borrowRecord.Status);
        Assert.Equal(0, borrowRecord.OverdueDays);
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
        Assert.Equal(0, borrowRecord.OverdueDays);

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

        var notification = Assert.Single(factory.BookStockChangeNotifications.Notifications);
        Assert.Equal(bookId, notification.BookId);
        Assert.Equal(2, notification.Stock);
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
        Assert.Equal(0, borrowRecord.OverdueDays);
    }

    [Fact]
    public async Task ReturnBook_WithOverdueBorrow_AllowsBorrowAfterReturn()
    {
        using var client = factory.CreateApiClient();
        var overdueBookId = await SeedBookAsync(name: "Return To Unblock Book", stock: 1);
        var targetBookId = await SeedBookAsync(name: "Borrow After Return Book", stock: 2);
        var dueDate = DateTime.UtcNow.Date.AddDays(-3);

        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            overdueBookId,
            borrowedAt: dueDate.AddDays(-BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: dueDate);

        var returnResponse = await client.PostAsync($"/api/return/{overdueBookId}", content: null);
        var borrowResponse = await client.PostAsync($"/api/borrow/{targetBookId}", content: null);

        Assert.Equal(HttpStatusCode.OK, returnResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, borrowResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var booksDbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var targetBookStock = await booksDbContext.Books
            .Where(book => book.Id == targetBookId)
            .Select(book => book.Stock)
            .SingleAsync();

        Assert.Equal(1, targetBookStock);
    }

    [Fact]
    public async Task ReturnBook_WithoutActiveBorrow_ReturnsExpectedError()
    {
        using var client = factory.CreateApiClient();
        var bookId = await SeedBookAsync(stock: 1);

        var response = await client.PostAsync($"/api/return/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(factory.BookStockChangeNotifications.Notifications);
    }

    [Fact]
    public async Task RenewBook_WithActiveBorrow_ExtendsDueDateAndIncrementsRenewalCount()
    {
        using var client = factory.CreateApiClient();
        var borrowedAt = CreateRecentBorrowedAt();
        var originalDueDate = borrowedAt.AddDays(BorrowingLoanPolicy.DefaultLoanPeriodDays);
        var bookId = await SeedBookAsync(name: "Renewable Book", stock: 2);

        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            bookId,
            borrowedAt: borrowedAt,
            dueDate: originalDueDate);

        var response = await client.PostAsync($"/api/borrow/renew/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecord = await response.Content.ReadFromJsonAsync<BorrowRecordResponse>();

        Assert.NotNull(borrowRecord);
        Assert.Equal(bookId, borrowRecord.BookId);
        Assert.Equal(originalDueDate.AddDays(BorrowingLoanPolicy.RenewalPeriodDays), borrowRecord.DueDate);
        Assert.Equal(1, borrowRecord.RenewalCount);
        Assert.Equal(nameof(BorrowStatus.Borrowed), borrowRecord.Status);

        using var scope = factory.Services.CreateScope();
        var borrowingDbContext = scope.ServiceProvider.GetRequiredService<BorrowingDbContext>();
        var storedBorrowRecord = await borrowingDbContext.BorrowRecords.SingleAsync();

        Assert.Equal(originalDueDate.AddDays(BorrowingLoanPolicy.RenewalPeriodDays), storedBorrowRecord.DueDate);
        Assert.Equal(1, storedBorrowRecord.RenewalCount);
    }

    [Fact]
    public async Task RenewBook_SecondRenewal_ReturnsBadRequest()
    {
        using var client = factory.CreateApiClient();
        var bookId = await SeedBookAsync(name: "Already Renewed Book", stock: 2);

        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            bookId,
            renewalCount: BorrowingLoanPolicy.MaxRenewalCount);

        var response = await client.PostAsync($"/api/borrow/renew/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RenewBook_WithOverdueBorrow_ReturnsBadRequest()
    {
        using var client = factory.CreateApiClient();
        var borrowedAt = DateTime.UtcNow.AddDays(-30);
        var bookId = await SeedBookAsync(name: "Overdue Renewal Book", stock: 2);

        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            bookId,
            borrowedAt: borrowedAt,
            dueDate: DateTime.UtcNow.AddDays(-1));

        var response = await client.PostAsync($"/api/borrow/renew/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RenewBook_WithReturnedBorrow_ReturnsNotFound()
    {
        using var client = factory.CreateApiClient();
        var borrowedAt = DateTime.UtcNow.AddDays(-3);
        var bookId = await SeedBookAsync(name: "Returned Renewal Book", stock: 2);

        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            bookId,
            borrowedAt: borrowedAt,
            returnedAt: borrowedAt.AddDays(1));

        var response = await client.PostAsync($"/api/borrow/renew/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RenewBook_ForAnotherUsersBorrow_ReturnsNotFound()
    {
        using var client = factory.CreateApiClient();
        var bookId = await SeedBookAsync(name: "Other User Renewal Book", stock: 2);

        await SeedBorrowRecordAsync("renewal-other-user", bookId);

        var response = await client.PostAsync($"/api/borrow/renew/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RenewBook_DoesNotChangeBookStock()
    {
        using var client = factory.CreateApiClient();
        const int stock = 4;
        var bookId = await SeedBookAsync(name: "Stock Neutral Renewal Book", stock: stock);

        await SeedBorrowRecordAsync(TestAuthenticationHandler.UserId, bookId);

        var response = await client.PostAsync($"/api/borrow/renew/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var booksDbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var storedStock = await booksDbContext.Books
            .Where(book => book.Id == bookId)
            .Select(book => book.Stock)
            .SingleAsync();

        Assert.Equal(stock, storedStock);
        Assert.Empty(factory.BookStockChangeNotifications.Notifications);
    }

    [Fact]
    public async Task GetMyBooks_AfterRenewal_ReturnsUpdatedDueDateAndRenewalCount()
    {
        using var client = factory.CreateApiClient();
        var borrowedAt = CreateRecentBorrowedAt();
        var originalDueDate = borrowedAt.AddDays(BorrowingLoanPolicy.DefaultLoanPeriodDays);
        var expectedDueDate = originalDueDate.AddDays(BorrowingLoanPolicy.RenewalPeriodDays);
        var bookId = await SeedBookAsync(name: "Renewed My Books Book", stock: 2);

        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            bookId,
            borrowedAt: borrowedAt,
            dueDate: originalDueDate);

        var renewResponse = await client.PostAsync($"/api/borrow/renew/{bookId}", content: null);
        renewResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/borrow/my-books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecords = await response.Content.ReadFromJsonAsync<List<BorrowRecordResponse>>();
        Assert.NotNull(borrowRecords);
        var borrowRecord = Assert.Single(borrowRecords);

        Assert.Equal(expectedDueDate, borrowRecord.DueDate);
        Assert.Equal(1, borrowRecord.RenewalCount);
    }

    [Fact]
    public async Task GetHistory_AfterRenewal_ReturnsUpdatedDueDateAndRenewalCount()
    {
        using var client = factory.CreateApiClient();
        var borrowedAt = CreateRecentBorrowedAt();
        var originalDueDate = borrowedAt.AddDays(BorrowingLoanPolicy.DefaultLoanPeriodDays);
        var expectedDueDate = originalDueDate.AddDays(BorrowingLoanPolicy.RenewalPeriodDays);
        var bookId = await SeedBookAsync(name: "Renewed History Book", stock: 2);

        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            bookId,
            borrowedAt: borrowedAt,
            dueDate: originalDueDate);

        var renewResponse = await client.PostAsync($"/api/borrow/renew/{bookId}", content: null);
        renewResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/borrow/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecords = await response.Content.ReadFromJsonAsync<List<BorrowRecordResponse>>();
        Assert.NotNull(borrowRecords);
        var borrowRecord = Assert.Single(borrowRecords);

        Assert.Equal(expectedDueDate, borrowRecord.DueDate);
        Assert.Equal(1, borrowRecord.RenewalCount);
    }

    [Fact]
    public async Task RenewBook_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = factory.CreateUnauthenticatedApiClient();
        var bookId = await SeedBookAsync(stock: 1);

        var response = await client.PostAsync($"/api/borrow/renew/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(IdentityRoles.Member)]
    [InlineData(IdentityRoles.Admin)]
    public async Task RenewBook_WithAuthenticatedRole_ReturnsSuccess(string role)
    {
        var userId = $"renew-{role.ToLowerInvariant()}-user";
        using var client = CreateAuthenticatedClient(role, userId);
        var bookId = await SeedBookAsync(name: $"Renew {role} Book", stock: 2);

        await SeedBorrowRecordAsync(userId, bookId);

        var response = await client.PostAsync($"/api/borrow/renew/{bookId}", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
    public async Task LibraryHub_Negotiate_WithoutAuthentication_ReturnsSuccess()
    {
        using var client = factory.CreateUnauthenticatedApiClient();

        var response = await client.PostAsync("/hubs/library/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
    public async Task GetHistory_WithOverdueBorrow_ReturnsOverdueDays()
    {
        using var client = factory.CreateApiClient();
        var dueDate = DateTime.UtcNow.Date.AddDays(-4);
        var bookId = await SeedBookAsync(name: "History Overdue Book", stock: 2);

        await SeedBorrowRecordAsync(
            TestAuthenticationHandler.UserId,
            bookId,
            borrowedAt: dueDate.AddDays(-BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: dueDate);

        var response = await client.GetAsync("/api/borrow/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var borrowRecords = await response.Content.ReadFromJsonAsync<List<BorrowRecordResponse>>();

        Assert.NotNull(borrowRecords);
        var borrowRecord = Assert.Single(borrowRecords);
        Assert.Equal(nameof(BorrowStatus.Overdue), borrowRecord.Status);
        Assert.Equal(4, borrowRecord.OverdueDays);
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

    [Fact]
    public async Task GetOverdue_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = factory.CreateUnauthenticatedApiClient();

        var response = await client.GetAsync("/api/borrow/overdue");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOverdue_WithMemberRole_ReturnsForbidden()
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Member, Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/borrow/overdue");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOverdue_WithAdminRole_ReturnsOnlyActiveOverdueBorrowsWithMetadata()
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Admin, Guid.NewGuid().ToString());
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        await SeedIdentityUserAsync(firstUserId, "overdue-reader-one");
        await SeedIdentityUserAsync(secondUserId, "overdue-reader-two");

        var oldestDueBookId = await SeedBookAsync(
            name: "Oldest Overdue Book",
            author: "First Author",
            stock: 2);
        var newerDueBookId = await SeedBookAsync(
            name: "Newer Overdue Book",
            author: "Second Author",
            stock: 2);
        var activeBookId = await SeedBookAsync(name: "Still Borrowed Book", stock: 2);
        var returnedBookId = await SeedBookAsync(name: "Returned Overdue Book", stock: 2);
        var oldestDueDate = DateTime.UtcNow.Date.AddDays(-5);
        var newerDueDate = DateTime.UtcNow.Date.AddDays(-2);

        await SeedBorrowRecordAsync(
            firstUserId.ToString(),
            oldestDueBookId,
            borrowedAt: oldestDueDate.AddDays(-BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: oldestDueDate,
            renewalCount: 1);
        await SeedBorrowRecordAsync(
            secondUserId.ToString(),
            newerDueBookId,
            borrowedAt: newerDueDate.AddDays(-BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: newerDueDate);
        await SeedBorrowRecordAsync(
            firstUserId.ToString(),
            activeBookId,
            dueDate: DateTime.UtcNow.AddDays(2));
        await SeedBorrowRecordAsync(
            secondUserId.ToString(),
            returnedBookId,
            borrowedAt: oldestDueDate.AddDays(-BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: oldestDueDate,
            returnedAt: DateTime.UtcNow.AddDays(-1));

        var response = await client.GetAsync("/api/borrow/overdue");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedOverdueBorrowRecordsResponse>();

        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal([oldestDueBookId, newerDueBookId], page.Items.Select(item => item.BookId));

        var oldestDueRecord = page.Items[0];
        Assert.Equal(firstUserId.ToString(), oldestDueRecord.UserId);
        Assert.Equal("overdue-reader-one", oldestDueRecord.Username);
        Assert.Equal("Oldest Overdue Book", oldestDueRecord.BookName);
        Assert.Equal("First Author", oldestDueRecord.Author);
        Assert.Equal(oldestDueDate, oldestDueRecord.DueDate);
        Assert.Equal(5, oldestDueRecord.OverdueDays);
        Assert.Equal(1, oldestDueRecord.RenewalCount);
        Assert.Equal(nameof(BorrowStatus.Overdue), oldestDueRecord.Status);

        Assert.DoesNotContain(page.Items, item => item.BookId == activeBookId);
        Assert.DoesNotContain(page.Items, item => item.BookId == returnedBookId);
    }

    [Fact]
    public async Task GetOverdue_WithPagination_ReturnsRequestedPageAndTotalCount()
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Admin, Guid.NewGuid().ToString());
        var userId = Guid.NewGuid();
        await SeedIdentityUserAsync(userId, "paged-overdue-reader");

        var firstBookId = await SeedBookAsync(name: "First Paged Overdue", stock: 2);
        var secondBookId = await SeedBookAsync(name: "Second Paged Overdue", stock: 2);
        var thirdBookId = await SeedBookAsync(name: "Third Paged Overdue", stock: 2);
        var baseDueDate = DateTime.UtcNow.Date.AddDays(-6);

        await SeedBorrowRecordAsync(
            userId.ToString(),
            firstBookId,
            borrowedAt: baseDueDate.AddDays(-BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: baseDueDate);
        await SeedBorrowRecordAsync(
            userId.ToString(),
            secondBookId,
            borrowedAt: baseDueDate.AddDays(1 - BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: baseDueDate.AddDays(1));
        await SeedBorrowRecordAsync(
            userId.ToString(),
            thirdBookId,
            borrowedAt: baseDueDate.AddDays(2 - BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: baseDueDate.AddDays(2));

        var response = await client.GetAsync("/api/borrow/overdue?page=2&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedOverdueBorrowRecordsResponse>();

        Assert.NotNull(page);
        Assert.Equal(2, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.TotalPages);

        var item = Assert.Single(page.Items);
        Assert.Equal(secondBookId, item.BookId);
    }

    [Theory]
    [InlineData("/api/borrow/overdue?page=0")]
    [InlineData("/api/borrow/overdue?pageSize=0")]
    [InlineData("/api/borrow/overdue?pageSize=101")]
    public async Task GetOverdue_WithInvalidPagination_ReturnsBadRequest(string url)
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Admin, Guid.NewGuid().ToString());

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
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

    private async Task SeedIdentityUserAsync(Guid id, string username)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await dbContext.Users.AddAsync(new ApplicationUser
        {
            Id = id,
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            Email = $"{username}@example.com",
            NormalizedEmail = $"{username}@example.com".ToUpperInvariant(),
            EmailConfirmed = true
        });
        await dbContext.SaveChangesAsync();
    }

    private static DateTime CreateRecentBorrowedAt()
    {
        var utcNow = DateTime.UtcNow;

        return new DateTime(
            utcNow.Year,
            utcNow.Month,
            utcNow.Day,
            utcNow.Hour,
            utcNow.Minute,
            0,
            DateTimeKind.Utc).AddDays(-1);
    }

    private async Task<Guid> SeedBorrowRecordAsync(
        string userId,
        Guid bookId,
        DateTime? borrowedAt = null,
        DateTime? dueDate = null,
        int renewalCount = 0,
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
            dueDate ?? resolvedBorrowedAt.AddDays(BorrowingLoanPolicy.DefaultLoanPeriodDays),
            renewalCount);

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
        string Status,
        int RenewalCount,
        int OverdueDays);

    private sealed record PagedOverdueBorrowRecordsResponse(
        IReadOnlyList<OverdueBorrowRecordResponse> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    private sealed record OverdueBorrowRecordResponse(
        Guid Id,
        string UserId,
        string Username,
        Guid BookId,
        string? BookName,
        string? Author,
        DateTime BorrowedAt,
        DateTime DueDate,
        int OverdueDays,
        int RenewalCount,
        string Status);

    private sealed record ProblemDetailsResponse(
        string? Title,
        string? Detail);
}
