using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LibrarySystem.Api.IntegrationTests.Infrastructure;

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

    private sealed record CreateBookRequest(string Name, string Author, int Stock);

    private sealed record BookResponse(Guid Id, string Name, string Author, int Stock);
}
