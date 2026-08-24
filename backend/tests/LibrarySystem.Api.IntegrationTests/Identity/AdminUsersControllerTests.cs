using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibrarySystem.Api.IntegrationTests.Infrastructure;
using LibrarySystem.Modules.Books.Domain;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Borrowing.Domain;
using LibrarySystem.Modules.Borrowing.Infrastructure;
using LibrarySystem.Modules.Identity.Domain;
using LibrarySystem.Shared.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibrarySystem.Api.IntegrationTests.Identity;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AdminUsersControllerTests(LibrarySystemApiFactory factory) : IAsyncLifetime
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
    public async Task GetUsers_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = factory.CreateUnauthenticatedApiClient();

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithMemberRole_ReturnsForbidden()
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Member);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithAdminRole_ReturnsDefaultPageOrderedByUsername()
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Admin);
        await CreateUserAsync("charlie", "charlie@example.com", IdentityRoles.Member);
        await CreateUserAsync("alpha", "alpha@example.com", IdentityRoles.Member);
        await CreateUserAsync("bravo", "bravo@example.com", IdentityRoles.Admin);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedUsersResponseAsync(response);

        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal(["alpha", "bravo", "charlie"], page.Items.Select(item => item.Username));
    }

    [Fact]
    public async Task GetUsers_WithCustomPagination_ReturnsRequestedPage()
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Admin);
        await CreateUserAsync("alpha-page", "alpha-page@example.com", IdentityRoles.Member);
        await CreateUserAsync("bravo-page", "bravo-page@example.com", IdentityRoles.Member);
        await CreateUserAsync("charlie-page", "charlie-page@example.com", IdentityRoles.Member);

        var response = await client.GetAsync("/api/admin/users?page=2&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedUsersResponseAsync(response);

        Assert.Equal(2, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.TotalPages);

        var item = Assert.Single(page.Items);
        Assert.Equal("bravo-page", item.Username);
    }

    [Theory]
    [InlineData("/api/admin/users?page=0")]
    [InlineData("/api/admin/users?pageSize=0")]
    [InlineData("/api/admin/users?pageSize=101")]
    public async Task GetUsers_WithInvalidPagination_ReturnsBadRequest(string url)
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Admin);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("mustafa", "mustafa")]
    [InlineData("search.email@example.com", "email-target")]
    [InlineData("MIXEDCASE", "mixedcase-user")]
    public async Task GetUsers_WithSearch_FiltersByUsernameOrEmailCaseInsensitive(
        string search,
        string expectedUsername)
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Admin);
        await CreateUserAsync("mustafa", "mustafa@example.com", IdentityRoles.Member);
        await CreateUserAsync("email-target", "search.email@example.com", IdentityRoles.Member);
        await CreateUserAsync("mixedcase-user", "mixedcase@example.com", IdentityRoles.Member);
        await CreateUserAsync("unmatched", "unmatched@example.com", IdentityRoles.Member);

        var response = await client.GetAsync($"/api/admin/users?search={Uri.EscapeDataString(search)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedUsersResponseAsync(response);

        var item = Assert.Single(page.Items);
        Assert.Equal(expectedUsername, item.Username);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task GetUsers_ReturnsRolesAndBorrowingCountsWithoutMixingUsers()
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Admin);
        var firstUserId = await CreateUserAsync(
            "borrow-summary-one",
            "borrow-summary-one@example.com",
            IdentityRoles.Member,
            IdentityRoles.Admin);
        var secondUserId = await CreateUserAsync(
            "borrow-summary-two",
            "borrow-summary-two@example.com",
            IdentityRoles.Member);
        var firstActiveBookId = await SeedBookAsync("First Active");
        var firstOverdueBookId = await SeedBookAsync("First Overdue");
        var firstReturnedBookId = await SeedBookAsync("First Returned");
        var secondActiveBookId = await SeedBookAsync("Second Active");
        var dueDate = DateTime.UtcNow.Date.AddDays(-3);

        await SeedBorrowRecordAsync(firstUserId, firstActiveBookId);
        await SeedBorrowRecordAsync(
            firstUserId,
            firstOverdueBookId,
            borrowedAt: dueDate.AddDays(-BorrowingLoanPolicy.DefaultLoanPeriodDays),
            dueDate: dueDate);
        await SeedBorrowRecordAsync(
            firstUserId,
            firstReturnedBookId,
            returnedAt: DateTime.UtcNow);
        await SeedBorrowRecordAsync(secondUserId, secondActiveBookId);

        var response = await client.GetAsync("/api/admin/users?search=borrow-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedUsersResponseAsync(response);

        Assert.Equal(2, page.TotalCount);

        var firstUser = Assert.Single(page.Items, item => item.Username == "borrow-summary-one");
        Assert.Equal(firstUserId, firstUser.Id);
        Assert.Equal(["Admin", "Member"], firstUser.Roles.Order(StringComparer.Ordinal));
        Assert.Equal(2, firstUser.ActiveBorrowCount);
        Assert.Equal(1, firstUser.OverdueBorrowCount);

        var secondUser = Assert.Single(page.Items, item => item.Username == "borrow-summary-two");
        Assert.Equal(secondUserId, secondUser.Id);
        Assert.Equal(["Member"], secondUser.Roles);
        Assert.Equal(1, secondUser.ActiveBorrowCount);
        Assert.Equal(0, secondUser.OverdueBorrowCount);
    }

    [Fact]
    public async Task GetUsers_DoesNotExposeSensitiveIdentityFields()
    {
        using var client = CreateAuthenticatedClient(IdentityRoles.Admin);
        await CreateUserAsync("safe-user", "safe-user@example.com", IdentityRoles.Member);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var item = document.RootElement.GetProperty("items")[0];

        Assert.False(item.TryGetProperty("passwordHash", out _));
        Assert.False(item.TryGetProperty("securityStamp", out _));
        Assert.False(item.TryGetProperty("concurrencyStamp", out _));
        Assert.False(item.TryGetProperty("phoneNumber", out _));
        Assert.False(item.TryGetProperty("lockoutEnd", out _));
        Assert.False(item.TryGetProperty("accessFailedCount", out _));
    }

    private async Task<string> CreateUserAsync(
        string username,
        string email,
        params string[] roles)
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

        foreach (var role in roles)
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        return user.Id.ToString();
    }

    private async Task<Guid> SeedBookAsync(string name)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var book = new Book(Guid.NewGuid(), name, "Test Author", stock: 2);

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

        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.HeaderName, $"admin-users-{role.ToLowerInvariant()}");
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeaderName, role);

        return client;
    }

    private static async Task<PagedAdminUsersResponse> ReadPagedUsersResponseAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<PagedAdminUsersResponse>()
            ?? throw new InvalidOperationException("Paged admin users response body was empty.");
    }

    private sealed record PagedAdminUsersResponse(
        IReadOnlyList<AdminUserResponse> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    private sealed record AdminUserResponse(
        string Id,
        string Username,
        string? Email,
        IReadOnlyList<string> Roles,
        int ActiveBorrowCount,
        int OverdueBorrowCount);
}
