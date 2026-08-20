using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LibrarySystem.Api.IntegrationTests.Infrastructure;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Identity.Domain;
using LibrarySystem.Shared.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibrarySystem.Api.IntegrationTests.Books;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class BooksControllerTests(LibrarySystemApiFactory factory) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await factory.ResetBooksDataAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetBooks_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = factory.CreateUnauthenticatedApiClient();

        var response = await client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBooks_WithEmptyDatabase_ReturnsOkWithEmptyArray()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var books = await response.Content.ReadFromJsonAsync<List<BookResponse>>();

        Assert.NotNull(books);
        Assert.Empty(books);
    }

    [Fact]
    public async Task BooksEndpoints_WithMemberJwt_AllowReadAndForbidWrites()
    {
        using var adminClient = await CreateAuthenticatedJwtClientAsync(IdentityRoles.Admin);
        var createdBook = await CreateBookAsync(adminClient);
        using var memberClient = await CreateAuthenticatedJwtClientAsync(IdentityRoles.Member);

        var getAllResponse = await memberClient.GetAsync("/api/books");
        var getByIdResponse = await memberClient.GetAsync($"/api/books/{createdBook.Id}");
        var createResponse = await memberClient.PostAsJsonAsync(
            "/api/books",
            new CreateBookRequest("Domain-Driven Design", "Eric Evans", 2));
        var updateResponse = await memberClient.PutAsJsonAsync(
            $"/api/books/{createdBook.Id}",
            new UpdateBookRequest("Refactoring", "Martin Fowler", 5));
        var deleteResponse = await memberClient.DeleteAsync($"/api/books/{createdBook.Id}");

        Assert.Equal(HttpStatusCode.OK, getAllResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getByIdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task BooksEndpoints_WithAdminJwt_AllowReadAndWrites()
    {
        using var client = await CreateAuthenticatedJwtClientAsync(IdentityRoles.Admin);
        var createRequest = new CreateBookRequest("Clean Architecture", "Robert C. Martin", 4);

        var createResponse = await client.PostAsJsonAsync("/api/books", createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdBook = await createResponse.Content.ReadFromJsonAsync<BookResponse>();

        Assert.NotNull(createdBook);

        var getResponse = await client.GetAsync($"/api/books/{createdBook.Id}");
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/books/{createdBook.Id}",
            new UpdateBookRequest("Refactoring", "Martin Fowler", 5));
        var deleteResponse = await client.DeleteAsync($"/api/books/{createdBook.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task CreateBook_WithValidRequest_ReturnsCreated()
    {
        using var client = factory.CreateApiClient();
        var request = new CreateBookRequest("Clean Code", "Robert C. Martin", 3);

        var response = await client.PostAsJsonAsync("/api/books", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var book = await response.Content.ReadFromJsonAsync<BookResponse>();

        Assert.NotNull(book);
        Assert.NotEqual(Guid.Empty, book.Id);
        Assert.Equal(request.Name, book.Name);
        Assert.Equal(request.Author, book.Author);
        Assert.Equal(request.Stock, book.Stock);
    }

    [Fact]
    public async Task GetBookById_WithExistingBook_ReturnsOkWithBook()
    {
        using var client = factory.CreateApiClient();
        var createdBook = await CreateBookAsync(client);

        var response = await client.GetAsync($"/api/books/{createdBook.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var book = await response.Content.ReadFromJsonAsync<BookResponse>();

        Assert.NotNull(book);
        Assert.Equal(createdBook.Id, book.Id);
        Assert.Equal(createdBook.Name, book.Name);
        Assert.Equal(createdBook.Author, book.Author);
        Assert.Equal(createdBook.Stock, book.Stock);
    }

    [Fact]
    public async Task DeleteBook_WithExistingBook_ReturnsNoContentAndThenGetReturnsNotFound()
    {
        using var client = factory.CreateApiClient();
        var createdBook = await CreateBookAsync(client);

        var deleteResponse = await client.DeleteAsync($"/api/books/{createdBook.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/books/{createdBook.Id}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateBook_WithValidRequest_ReturnsOkAndUpdatesBook()
    {
        using var client = factory.CreateApiClient();
        var createdBook = await CreateBookAsync(client);
        var request = new UpdateBookRequest("Refactoring", "Martin Fowler", 5);

        var response = await client.PutAsJsonAsync($"/api/books/{createdBook.Id}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedBook = await response.Content.ReadFromJsonAsync<BookResponse>();

        Assert.NotNull(updatedBook);
        Assert.Equal(createdBook.Id, updatedBook.Id);
        Assert.Equal(request.Name, updatedBook.Name);
        Assert.Equal(request.Author, updatedBook.Author);
        Assert.Equal(request.Stock, updatedBook.Stock);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var storedBook = await dbContext.Books
            .AsNoTracking()
            .SingleAsync(book => book.Id == createdBook.Id);

        Assert.Equal(request.Name, storedBook.Name);
        Assert.Equal(request.Author, storedBook.Author);
        Assert.Equal(request.Stock, storedBook.Stock);
    }

    [Fact]
    public async Task UpdateBook_WithUnknownId_ReturnsNotFound()
    {
        using var client = factory.CreateApiClient();
        var unknownId = Guid.NewGuid();
        var request = new UpdateBookRequest("Refactoring", "Martin Fowler", 5);

        var response = await client.PutAsJsonAsync($"/api/books/{unknownId}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UpdateBook_WithInvalidRequest_ReturnsBadRequest()
    {
        using var client = factory.CreateApiClient();
        var createdBook = await CreateBookAsync(client);
        var request = new UpdateBookRequest(string.Empty, string.Empty, -1);

        var response = await client.PutAsJsonAsync($"/api/books/{createdBook.Id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var content = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var errors = content.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("Name", out var nameErrors));
        Assert.True(nameErrors.GetArrayLength() > 0);
        Assert.True(errors.TryGetProperty("Author", out var authorErrors));
        Assert.True(authorErrors.GetArrayLength() > 0);
        Assert.True(errors.TryGetProperty("Stock", out var stockErrors));
        Assert.True(stockErrors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task UpdateBook_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = factory.CreateUnauthenticatedApiClient();
        var unknownId = Guid.NewGuid();
        var request = new UpdateBookRequest("Refactoring", "Martin Fowler", 5);

        var response = await client.PutAsJsonAsync($"/api/books/{unknownId}", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateBook_WithInvalidRequest_ReturnsBadRequestWithValidationErrors()
    {
        using var client = factory.CreateApiClient();
        var request = new CreateBookRequest(string.Empty, string.Empty, -1);

        var response = await client.PostAsJsonAsync("/api/books", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var content = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var errors = content.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("Name", out var nameErrors));
        Assert.True(nameErrors.GetArrayLength() > 0);
        Assert.True(errors.TryGetProperty("Author", out var authorErrors));
        Assert.True(authorErrors.GetArrayLength() > 0);
        Assert.True(errors.TryGetProperty("Stock", out var stockErrors));
        Assert.True(stockErrors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetBookById_WithUnknownId_ReturnsNotFound()
    {
        using var client = factory.CreateApiClient();
        var unknownId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/books/{unknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<BookResponse> CreateBookAsync(HttpClient client)
    {
        var request = new CreateBookRequest("Clean Code", "Robert C. Martin", 3);
        var response = await client.PostAsJsonAsync("/api/books", request);

        response.EnsureSuccessStatusCode();

        var book = await response.Content.ReadFromJsonAsync<BookResponse>();

        return book ?? throw new InvalidOperationException("Create book response body was empty.");
    }

    private async Task<HttpClient> CreateAuthenticatedJwtClientAsync(string role)
    {
        var credentials = CreateUserCredentials();
        AuthResponse authResponse;

        if (role == IdentityRoles.Member)
        {
            using var anonymousClient = factory.CreateUnauthenticatedApiClient();
            var registerResponse = await anonymousClient.PostAsJsonAsync(
                "/api/auth/register",
                new RegisterRequest(credentials.Username, credentials.Email, credentials.Password));

            registerResponse.EnsureSuccessStatusCode();

            authResponse = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>()
                ?? throw new InvalidOperationException("Register response body was empty.");
        }
        else if (role == IdentityRoles.Admin)
        {
            await CreateIdentityUserInRoleAsync(credentials, IdentityRoles.Admin);

            using var anonymousClient = factory.CreateUnauthenticatedApiClient();
            var loginResponse = await anonymousClient.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(credentials.Username, credentials.Password));

            loginResponse.EnsureSuccessStatusCode();

            authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>()
                ?? throw new InvalidOperationException("Login response body was empty.");
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported test role.");
        }

        var client = factory.CreateUnauthenticatedApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            authResponse.TokenType,
            authResponse.AccessToken);

        return client;
    }

    private async Task CreateIdentityUserInRoleAsync(UserCredentials credentials, string role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = credentials.Username,
            Email = credentials.Email
        };

        var createResult = await userManager.CreateAsync(user, credentials.Password);

        Assert.True(createResult.Succeeded);

        var roleResult = await userManager.AddToRoleAsync(user, role);

        Assert.True(roleResult.Succeeded);
    }

    private static UserCredentials CreateUserCredentials()
    {
        var uniqueValue = Guid.NewGuid().ToString("N");

        return new UserCredentials(
            $"user-{uniqueValue}",
            $"user-{uniqueValue}@example.test",
            "ValidPassword123!");
    }

    private sealed record CreateBookRequest(string Name, string Author, int Stock);

    private sealed record UpdateBookRequest(string Name, string Author, int Stock);

    private sealed record BookResponse(Guid Id, string Name, string Author, int Stock);

    private sealed record RegisterRequest(string Username, string Email, string Password);

    private sealed record LoginRequest(string Username, string Password);

    private sealed record AuthResponse(string AccessToken, int ExpiresIn, string TokenType);

    private sealed record UserCredentials(string Username, string Email, string Password);
}
