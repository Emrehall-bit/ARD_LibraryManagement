using System.Net;
using System.Net.Http.Json;
using LibrarySystem.Api.IntegrationTests.Infrastructure;
using LibrarySystem.Modules.Books.Domain;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Borrowing.Domain;
using LibrarySystem.Modules.Borrowing.Infrastructure;
using LibrarySystem.Modules.Identity.Domain;
using LibrarySystem.Shared.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LibrarySystem.Api.IntegrationTests.AdminDashboard;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AdminDashboardControllerTests(LibrarySystemApiFactory factory) : IAsyncLifetime
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
    public async Task GetDashboard_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = factory.CreateUnauthenticatedApiClient();

        var response = await client.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_WithMemberRole_ReturnsForbidden()
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Member);

        var response = await client.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_WithAdminRoleAndEmptyData_ReturnsZeroCounts()
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Admin);

        var response = await client.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dashboard = await ReadDashboardResponseAsync(response);

        Assert.Equal(0, dashboard.TotalUsers);
        Assert.Equal(0, dashboard.TotalBooks);
        Assert.Equal(0, dashboard.TotalStock);
        Assert.Equal(0, dashboard.OutOfStockBooks);
        Assert.Equal(0, dashboard.ActiveBorrows);
        Assert.Equal(0, dashboard.OverdueBorrows);
        Assert.Equal(0, dashboard.ReturnedBorrows);
        Assert.Empty(dashboard.RecentOverdueBorrows);
    }

    [Fact]
    public async Task GetDashboard_WithAdminRole_ReturnsMetricsAndRecentOverdueBorrows()
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Admin);
        var firstUserId = await CreateUserAsync("dashboard-reader-one", "dashboard-one@example.com");
        var secondUserId = await CreateUserAsync("dashboard-reader-two", "dashboard-two@example.com");
        var thirdUserId = await CreateUserAsync("dashboard-reader-three", "dashboard-three@example.com");
        var oldestBookId = await SeedBookAsync("Oldest Dashboard Overdue", "First Author", stock: 0);
        var secondBookId = await SeedBookAsync("Second Dashboard Overdue", "Second Author", stock: 3);
        var thirdBookId = await SeedBookAsync("Third Dashboard Overdue", "Third Author", stock: 7);
        var fourthBookId = await SeedBookAsync("Fourth Dashboard Overdue", "Fourth Author", stock: 1);
        var fifthBookId = await SeedBookAsync("Fifth Dashboard Overdue", "Fifth Author", stock: 4);
        var sixthBookId = await SeedBookAsync("Sixth Dashboard Overdue", "Sixth Author", stock: 2);
        var activeBookId = await SeedBookAsync("Dashboard Active Borrow", "Active Author", stock: 8);
        var returnedBookId = await SeedBookAsync("Dashboard Returned Borrow", "Returned Author", stock: 5);
        var dueBase = DateTime.UtcNow.Date.AddDays(-12);

        await SeedBorrowRecordAsync(
            firstUserId,
            oldestBookId,
            borrowedAt: dueBase.AddDays(-BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: dueBase);
        await SeedBorrowRecordAsync(
            secondUserId,
            secondBookId,
            borrowedAt: dueBase.AddDays(1 - BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: dueBase.AddDays(1));
        await SeedBorrowRecordAsync(
            thirdUserId,
            thirdBookId,
            borrowedAt: dueBase.AddDays(2 - BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: dueBase.AddDays(2));
        await SeedBorrowRecordAsync(
            firstUserId,
            fourthBookId,
            borrowedAt: dueBase.AddDays(3 - BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: dueBase.AddDays(3));
        await SeedBorrowRecordAsync(
            secondUserId,
            fifthBookId,
            borrowedAt: dueBase.AddDays(4 - BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: dueBase.AddDays(4));
        await SeedBorrowRecordAsync(
            thirdUserId,
            sixthBookId,
            borrowedAt: dueBase.AddDays(5 - BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: dueBase.AddDays(5));
        await SeedBorrowRecordAsync(
            firstUserId,
            activeBookId,
            dueDate: DateTime.UtcNow.Date.AddDays(3));
        await SeedBorrowRecordAsync(
            secondUserId,
            returnedBookId,
            borrowedAt: dueBase.AddDays(-BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: dueBase,
            returnedAt: DateTime.UtcNow.AddDays(-1));

        var response = await client.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dashboard = await ReadDashboardResponseAsync(response);

        Assert.Equal(3, dashboard.TotalUsers);
        Assert.Equal(8, dashboard.TotalBooks);
        Assert.Equal(30, dashboard.TotalStock);
        Assert.Equal(1, dashboard.OutOfStockBooks);
        Assert.Equal(7, dashboard.ActiveBorrows);
        Assert.Equal(6, dashboard.OverdueBorrows);
        Assert.Equal(1, dashboard.ReturnedBorrows);
        Assert.Equal(5, dashboard.RecentOverdueBorrows.Count);
        Assert.Equal(
            [oldestBookId, secondBookId, thirdBookId, fourthBookId, fifthBookId],
            dashboard.RecentOverdueBorrows.Select(item => item.BookId));

        var oldest = dashboard.RecentOverdueBorrows[0];
        Assert.Equal(firstUserId, oldest.UserId);
        Assert.Equal("dashboard-reader-one", oldest.Username);
        Assert.Equal(oldestBookId, oldest.BookId);
        Assert.Equal("Oldest Dashboard Overdue", oldest.BookName);
        Assert.Equal("First Author", oldest.Author);
        Assert.Equal(dueBase, oldest.DueDate);
        Assert.Equal(12, oldest.OverdueDays);

        Assert.DoesNotContain(dashboard.RecentOverdueBorrows, item => item.BookId == sixthBookId);
        Assert.DoesNotContain(dashboard.RecentOverdueBorrows, item => item.BookId == activeBookId);
        Assert.DoesNotContain(dashboard.RecentOverdueBorrows, item => item.BookId == returnedBookId);
    }

    private async Task<string> CreateUserAsync(string username, string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = username,
            Email = email,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, "Password123!");
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, IdentityRoles.Member);
        Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));

        return user.Id.ToString();
    }

    private async Task<Guid> SeedBookAsync(
        string name,
        string author,
        int stock)
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
    }

    private HttpClient CreateAuthenticatedClient(string role)
    {
        var client = factory.CreateUnauthenticatedApiClient();

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.HeaderName, $"admin-dashboard-{role.ToLowerInvariant()}");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeaderName, role);

        return client;
    }

    private static async Task<AdminDashboardResponse> ReadDashboardResponseAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<AdminDashboardResponse>()
            ?? throw new InvalidOperationException("Admin dashboard response body was empty.");
    }

    private sealed record AdminDashboardResponse(
        int TotalUsers,
        int TotalBooks,
        int TotalStock,
        int OutOfStockBooks,
        int ActiveBorrows,
        int OverdueBorrows,
        int ReturnedBorrows,
        IReadOnlyList<RecentOverdueBorrowResponse> RecentOverdueBorrows);

    private sealed record RecentOverdueBorrowResponse(
        Guid Id,
        string UserId,
        string Username,
        Guid BookId,
        string? BookName,
        string? Author,
        DateTime DueDate,
        int OverdueDays);
}
