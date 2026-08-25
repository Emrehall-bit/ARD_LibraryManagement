using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LibrarySystem.Api.IntegrationTests.Infrastructure;
using LibrarySystem.Modules.Books.Domain;
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
    public async Task GetBooks_WithoutAuthentication_ReturnsOk()
    {
        using var client = factory.CreateUnauthenticatedApiClient();

        var response = await client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBookById_WithoutAuthentication_ReturnsOk()
    {
        using var authenticatedClient = factory.CreateApiClient();
        var createdBook = await CreateBookAsync(authenticatedClient);
        using var anonymousClient = factory.CreateUnauthenticatedApiClient();

        var response = await anonymousClient.GetAsync($"/api/books/{createdBook.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var book = await response.Content.ReadFromJsonAsync<BookResponse>();

        Assert.NotNull(book);
        Assert.Equal(createdBook.Id, book.Id);
    }

    [Fact]
    public async Task GetBooks_WithEmptyDatabase_ReturnsOkWithEmptyArray()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var books = await response.Content.ReadFromJsonAsync<PagedBooksResponse>();

        Assert.NotNull(books);
        Assert.Empty(books.Items);
        Assert.Equal(1, books.Page);
        Assert.Equal(20, books.PageSize);
        Assert.Equal(0, books.TotalCount);
        Assert.Equal(0, books.TotalPages);
    }

    [Fact]
    public async Task GetBooks_WithDefaultQuery_ReturnsFirstPageWithDefaultPageSize()
    {
        using var client = factory.CreateApiClient();
        await SeedBooksAsync(25);

        var response = await client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(25, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(20, page.Items.Count);
        Assert.Equal("Book 001", page.Items[0].Name);
        Assert.Equal("Book 020", page.Items[^1].Name);
    }

    [Fact]
    public async Task GetBooks_WithPageTwo_ReturnsSecondPage()
    {
        using var client = factory.CreateApiClient();
        await SeedBooksAsync(25);

        var response = await client.GetAsync("/api/books?page=2&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(2, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(25, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(5, page.Items.Count);
        Assert.Equal("Book 021", page.Items[0].Name);
        Assert.Equal("Book 025", page.Items[^1].Name);
    }

    [Fact]
    public async Task GetBooks_WithCustomPageSize_ReturnsRequestedPageSize()
    {
        using var client = factory.CreateApiClient();
        await SeedBooksAsync(12);

        var response = await client.GetAsync("/api/books?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(1, page.Page);
        Assert.Equal(5, page.PageSize);
        Assert.Equal(12, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(5, page.Items.Count);
    }

    [Fact]
    public async Task GetBooks_WithSearchByName_ReturnsMatchingBooks()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Foundation Patterns", "Test Author", 2);
        await SeedBookAsync("Clean Code", "Robert Martin", 2);

        var response = await client.GetAsync("/api/books?search=Foundation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);
        var book = Assert.Single(page.Items);

        Assert.Equal("Foundation Patterns", book.Name);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task GetBooks_WithSearchByAuthor_ReturnsMatchingBooks()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Book A", "Isaac Field", 2);
        await SeedBookAsync("Book B", "Another Author", 2);

        var response = await client.GetAsync("/api/books?search=Isaac");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);
        var book = Assert.Single(page.Items);

        Assert.Equal("Isaac Field", book.Author);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task GetBooks_WithSearch_IsCaseInsensitive()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Mixed Case Catalogue", "Search Author", 2);

        var response = await client.GetAsync("/api/books?search=mIxEd");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);
        var book = Assert.Single(page.Items);

        Assert.Equal("Mixed Case Catalogue", book.Name);
    }

    [Fact]
    public async Task GetBooks_WithWhitespaceSearch_DoesNotFilter()
    {
        using var client = factory.CreateApiClient();
        await SeedBooksAsync(25);

        var response = await client.GetAsync("/api/books?search=%20%20%20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(25, page.TotalCount);
        Assert.Equal(20, page.Items.Count);
    }

    [Fact]
    public async Task GetBooks_WithDefaultSorting_ReturnsNameAscending()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Charlie", "Author B", 2);
        await SeedBookAsync("Alpha", "Author A", 3);
        await SeedBookAsync("Bravo", "Author C", 1);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        AssertBookNames(page, ["Alpha", "Bravo", "Charlie"]);
    }

    [Fact]
    public async Task GetBooks_WithNameDescendingSorting_ReturnsNameDescending()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Charlie", "Author B", 2);
        await SeedBookAsync("Alpha", "Author A", 3);
        await SeedBookAsync("Bravo", "Author C", 1);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10&sortBy=name&sortDirection=desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        AssertBookNames(page, ["Charlie", "Bravo", "Alpha"]);
    }

    [Fact]
    public async Task GetBooks_WithAuthorAscendingSorting_ReturnsAuthorAscending()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Gamma", "Carver", 2);
        await SeedBookAsync("Alpha", "Borges", 3);
        await SeedBookAsync("Beta", "Adams", 1);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10&sortBy=author&sortDirection=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        AssertBookNames(page, ["Beta", "Alpha", "Gamma"]);
    }

    [Fact]
    public async Task GetBooks_WithAuthorDescendingSorting_ReturnsAuthorDescending()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Gamma", "Carver", 2);
        await SeedBookAsync("Alpha", "Borges", 3);
        await SeedBookAsync("Beta", "Adams", 1);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10&sortBy=author&sortDirection=desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        AssertBookNames(page, ["Gamma", "Alpha", "Beta"]);
    }

    [Fact]
    public async Task GetBooks_WithStockAscendingSorting_ReturnsStockAscending()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Delta", "Author", 3);
        await SeedBookAsync("Alpha", "Author", 1);
        await SeedBookAsync("Charlie", "Author", 1);
        await SeedBookAsync("Bravo", "Author", 2);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10&sortBy=stock&sortDirection=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        AssertBookNames(page, ["Alpha", "Charlie", "Bravo", "Delta"]);
    }

    [Fact]
    public async Task GetBooks_WithStockDescendingSorting_ReturnsStockDescending()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Delta", "Author", 3);
        await SeedBookAsync("Alpha", "Author", 1);
        await SeedBookAsync("Charlie", "Author", 1);
        await SeedBookAsync("Bravo", "Author", 2);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10&sortBy=stock&sortDirection=desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        AssertBookNames(page, ["Delta", "Bravo", "Alpha", "Charlie"]);
    }

    [Fact]
    public async Task GetBooks_WithSortingAndPagination_ReturnsRequestedSortedPage()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Alpha", "Author", 1);
        await SeedBookAsync("Bravo", "Author", 1);
        await SeedBookAsync("Charlie", "Author", 1);
        await SeedBookAsync("Delta", "Author", 1);
        await SeedBookAsync("Echo", "Author", 1);

        var response = await client.GetAsync("/api/books?page=2&pageSize=2&sortBy=name&sortDirection=desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        AssertBookNames(page, ["Charlie", "Bravo"]);
    }

    [Fact]
    public async Task GetBooks_WithSortingAndSearch_ReturnsMatchingSortedBooks()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Zed", "George Orwell", 1);
        await SeedBookAsync("Animal Farm", "George Orwell", 1);
        await SeedBookAsync("Other", "No Match", 1);

        var response = await client.GetAsync(
            "/api/books?page=1&pageSize=10&search=orwell&sortBy=name&sortDirection=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(2, page.TotalCount);
        AssertBookNames(page, ["Animal Farm", "Zed"]);
    }

    [Theory]
    [InlineData("/api/books?sortBy=publishedAt")]
    [InlineData("/api/books?sortBy=%20%20%20")]
    public async Task GetBooks_WithInvalidSortBy_ReturnsBadRequest(string url)
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/books?sortDirection=ascending")]
    [InlineData("/api/books?sortDirection=%20%20%20")]
    public async Task GetBooks_WithInvalidSortDirection_ReturnsBadRequest(string url)
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetBooks_WithCaseInsensitiveSortingValues_ReturnsSortedBooks()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Low", "Author", 1);
        await SeedBookAsync("High", "Author", 9);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10&sortBy=StOcK&sortDirection=DeSc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        AssertBookNames(page, ["High", "Low"]);
    }

    [Fact]
    public async Task GetBooks_WithDefaultStockStatus_ReturnsAllBooks()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("In Stock", "Author", 3);
        await SeedBookAsync("Out Of Stock", "Author", 0);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(2, page.TotalCount);
        AssertBookNames(page, ["In Stock", "Out Of Stock"]);
    }

    [Fact]
    public async Task GetBooks_WithInStockFilter_ReturnsOnlyBooksWithPositiveStock()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Available One", "Author", 1);
        await SeedBookAsync("Unavailable", "Author", 0);
        await SeedBookAsync("Available Two", "Author", 5);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10&stockStatus=inStock");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, book => Assert.True(book.Stock > 0));
        AssertBookNames(page, ["Available One", "Available Two"]);
    }

    [Fact]
    public async Task GetBooks_WithOutOfStockFilter_ReturnsOnlyBooksWithZeroStock()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Available", "Author", 2);
        await SeedBookAsync("Gone One", "Author", 0);
        await SeedBookAsync("Gone Two", "Author", 0);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10&stockStatus=outOfStock");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, book => Assert.Equal(0, book.Stock));
        AssertBookNames(page, ["Gone One", "Gone Two"]);
    }

    [Fact]
    public async Task GetBooks_WithCaseInsensitiveStockStatus_ReturnsFilteredBooks()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Available", "Author", 2);
        await SeedBookAsync("Gone", "Author", 0);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10&stockStatus=OuToFsToCk");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        var book = Assert.Single(page.Items);

        Assert.Equal("Gone", book.Name);
        Assert.Equal(0, book.Stock);
    }

    [Theory]
    [InlineData("/api/books?stockStatus=available123")]
    [InlineData("/api/books?stockStatus=%20%20%20")]
    public async Task GetBooks_WithInvalidStockStatus_ReturnsBadRequest(string url)
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetBooks_WithStockFilterAndSearch_ReturnsMatchingFilteredBooks()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Animal Farm", "George Orwell", 4);
        await SeedBookAsync("Nineteen Eighty-Four", "George Orwell", 0);
        await SeedBookAsync("Other", "No Match", 7);

        var response = await client.GetAsync(
            "/api/books?page=1&pageSize=10&search=orwell&stockStatus=inStock");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        var book = Assert.Single(page.Items);

        Assert.Equal("Animal Farm", book.Name);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task GetBooks_WithStockFilterAndSorting_ReturnsFilteredSortedBooks()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Low", "Author", 1);
        await SeedBookAsync("Gone", "Author", 0);
        await SeedBookAsync("High", "Author", 9);

        var response = await client.GetAsync(
            "/api/books?page=1&pageSize=10&stockStatus=inStock&sortBy=stock&sortDirection=desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(2, page.TotalCount);
        AssertBookNames(page, ["High", "Low"]);
    }

    [Fact]
    public async Task GetBooks_WithStockFilterAndPagination_ReturnsFilteredTotalCount()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Alpha", "Author", 1);
        await SeedBookAsync("Bravo", "Author", 0);
        await SeedBookAsync("Charlie", "Author", 2);
        await SeedBookAsync("Delta", "Author", 0);
        await SeedBookAsync("Echo", "Author", 3);

        var response = await client.GetAsync(
            "/api/books?page=2&pageSize=2&stockStatus=inStock&sortBy=name&sortDirection=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        AssertBookNames(page, ["Echo"]);
    }

    [Fact]
    public async Task GetBooks_WithCategoryFilter_ReturnsOnlyMatchingCategory()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Dune", "Frank Herbert", 4, BookCategory.ScienceFiction);
        await SeedBookAsync("Foundation", "Isaac Asimov", 2, BookCategory.ScienceFiction);
        await SeedBookAsync("Hamlet", "William Shakespeare", 1, BookCategory.Poetry);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10&category=ScienceFiction");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, book => Assert.Equal(nameof(BookCategory.ScienceFiction), book.Category));
        AssertBookNames(page, ["Dune", "Foundation"]);
    }

    [Fact]
    public async Task GetBooks_WithCaseInsensitiveCategory_ReturnsMatchingBooks()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Dune", "Frank Herbert", 4, BookCategory.ScienceFiction);
        await SeedBookAsync("Hamlet", "William Shakespeare", 1, BookCategory.Poetry);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10&category=sciencefiction");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);
        var book = Assert.Single(page.Items);

        Assert.Equal("Dune", book.Name);
        Assert.Equal(nameof(BookCategory.ScienceFiction), book.Category);
    }

    [Theory]
    [InlineData("/api/books?category=Kitchen")]
    public async Task GetBooks_WithInvalidCategory_ReturnsBadRequest(string url)
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetBooks_WithCategoryAndSearch_ReturnsMatchingFilteredBooks()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Dune", "Frank Herbert", 4, BookCategory.ScienceFiction);
        await SeedBookAsync("Dune Poems", "Frank Herbert", 4, BookCategory.Poetry);
        await SeedBookAsync("Foundation", "Isaac Asimov", 2, BookCategory.ScienceFiction);

        var response = await client.GetAsync(
            "/api/books?page=1&pageSize=10&search=dune&category=ScienceFiction");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);
        var book = Assert.Single(page.Items);

        Assert.Equal("Dune", book.Name);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task GetBooks_WithCategoryAndStockFilter_ReturnsMatchingFilteredBooks()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Available Fantasy", "Author", 3, BookCategory.Fantasy);
        await SeedBookAsync("Unavailable Fantasy", "Author", 0, BookCategory.Fantasy);
        await SeedBookAsync("Available Novel", "Author", 3, BookCategory.Novel);

        var response = await client.GetAsync(
            "/api/books?page=1&pageSize=10&category=Fantasy&stockStatus=inStock");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);
        var book = Assert.Single(page.Items);

        Assert.Equal("Available Fantasy", book.Name);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task GetBooks_WithCategoryAndSorting_ReturnsMatchingSortedBooks()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Low", "Author", 1, BookCategory.History);
        await SeedBookAsync("High", "Author", 9, BookCategory.History);
        await SeedBookAsync("Other", "Author", 20, BookCategory.Science);

        var response = await client.GetAsync(
            "/api/books?page=1&pageSize=10&category=History&sortBy=stock&sortDirection=desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(2, page.TotalCount);
        AssertBookNames(page, ["High", "Low"]);
    }

    [Fact]
    public async Task GetBooks_WithCategoryAndPagination_ReturnsFilteredTotalCount()
    {
        using var client = factory.CreateApiClient();
        await SeedBookAsync("Alpha", "Author", 1, BookCategory.Biography);
        await SeedBookAsync("Bravo", "Author", 1, BookCategory.Biography);
        await SeedBookAsync("Charlie", "Author", 1, BookCategory.Biography);
        await SeedBookAsync("Delta", "Author", 1, BookCategory.Mystery);

        var response = await client.GetAsync(
            "/api/books?page=2&pageSize=2&category=Biography&sortBy=name&sortDirection=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await ReadPagedBooksResponseAsync(response);

        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        AssertBookNames(page, ["Charlie"]);
    }

    [Theory]
    [InlineData("/api/books?page=0")]
    [InlineData("/api/books?pageSize=0")]
    [InlineData("/api/books?pageSize=101")]
    public async Task GetBooks_WithInvalidPagination_ReturnsBadRequest(string url)
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BooksEndpoints_WithMemberJwt_AllowReadAndForbidWrites()
    {
        using var adminClient = await CreateAuthenticatedJwtClientAsync(IdentityRoles.Admin);
        var createdBook = await CreateBookAsync(adminClient);
        using var memberClient = await CreateAuthenticatedJwtClientAsync(IdentityRoles.Member);

        var getAllResponse = await memberClient.GetAsync("/api/books?sortBy=name&sortDirection=asc");
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

        var getAllResponse = await client.GetAsync("/api/books?sortBy=name&sortDirection=asc");
        var getResponse = await client.GetAsync($"/api/books/{createdBook.Id}");
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/books/{createdBook.Id}",
            new UpdateBookRequest("Refactoring", "Martin Fowler", 5));
        var deleteResponse = await client.DeleteAsync($"/api/books/{createdBook.Id}");

        Assert.Equal(HttpStatusCode.OK, getAllResponse.StatusCode);
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
        Assert.Equal(request.Category, book.Category);
    }

    [Fact]
    public async Task CreateBook_WithOptionalMetadata_ReturnsCreatedAndPersistsMetadata()
    {
        using var client = factory.CreateApiClient();
        var request = new CreateBookRequest(
            "Clean Architecture",
            "Robert C. Martin",
            4,
            nameof(BookCategory.Science),
            "Architecture guidance for software systems.",
            "978-0134494166",
            "Prentice Hall",
            2017);

        var response = await client.PostAsJsonAsync("/api/books", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var book = await response.Content.ReadFromJsonAsync<BookResponse>();

        Assert.NotNull(book);
        Assert.Equal(request.Description, book.Description);
        Assert.Equal(request.Isbn, book.Isbn);
        Assert.Equal(request.Publisher, book.Publisher);
        Assert.Equal(request.PublishedYear, book.PublishedYear);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var storedBook = await dbContext.Books
            .AsNoTracking()
            .SingleAsync(storedBook => storedBook.Id == book.Id);

        Assert.Equal(request.Description, storedBook.Description);
        Assert.Equal(request.Isbn, storedBook.Isbn);
        Assert.Equal(request.Publisher, storedBook.Publisher);
        Assert.Equal(request.PublishedYear, storedBook.PublishedYear);
    }

    [Fact]
    public async Task CreateBook_WithNullMetadata_ReturnsNullMetadata()
    {
        using var client = factory.CreateApiClient();
        var request = new CreateBookRequest("Null Metadata Book", "Optional Author", 2);

        var response = await client.PostAsJsonAsync("/api/books", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var book = await response.Content.ReadFromJsonAsync<BookResponse>();

        Assert.NotNull(book);
        Assert.Null(book.Description);
        Assert.Null(book.Isbn);
        Assert.Null(book.Publisher);
        Assert.Null(book.PublishedYear);
    }

    [Fact]
    public async Task CreateBook_WithInvalidCategory_ReturnsBadRequest()
    {
        using var client = factory.CreateApiClient();
        var request = new CreateBookRequest("Clean Code", "Robert C. Martin", 3, "NotACategory");

        var response = await client.PostAsJsonAsync("/api/books", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var content = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var errors = content.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("Category", out var categoryErrors));
        Assert.True(categoryErrors.GetArrayLength() > 0);
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
        Assert.Equal(createdBook.Category, book.Category);
    }

    [Fact]
    public async Task GetBookById_WithMetadataAndImages_ReturnsDetailContractWithDeterministicImageOrder()
    {
        using var client = factory.CreateApiClient();
        var createdBook = await CreateBookAsync(
            client,
            new CreateBookRequest(
                "Image Detail Book",
                "Detail Author",
                3,
                nameof(BookCategory.Science),
                "A richly illustrated book.",
                "978-0000000001",
                "Gallery Press",
                DateTime.UtcNow.Year));
        var firstGalleryImageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondGalleryImageId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var coverImageId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        await SeedBookImageAsync(secondGalleryImageId, createdBook.Id, "books/gallery-later.webp", false, 1);
        await SeedBookImageAsync(coverImageId, createdBook.Id, $"books/{createdBook.Id}/cover.webp", true, 99);
        await SeedBookImageAsync(firstGalleryImageId, createdBook.Id, "books/gallery-first.webp", false, 1);

        var response = await client.GetAsync($"/api/books/{createdBook.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var book = await response.Content.ReadFromJsonAsync<BookDetailResponse>();

        Assert.NotNull(book);
        Assert.Equal(createdBook.Description, book.Description);
        Assert.Equal(createdBook.Isbn, book.Isbn);
        Assert.Equal(createdBook.Publisher, book.Publisher);
        Assert.Equal(createdBook.PublishedYear, book.PublishedYear);
        Assert.Equal(
            [coverImageId, firstGalleryImageId, secondGalleryImageId],
            book.Images.Select(image => image.Id).ToList());
        Assert.True(book.Images[0].IsCover);
    }

    [Fact]
    public async Task GetBooks_DoesNotReturnImageMetadataForListItems()
    {
        using var client = factory.CreateApiClient();
        var bookId = await SeedBookAsync("List Contract Book", "List Author", 2);
        await SeedBookImageAsync(Guid.NewGuid(), bookId, $"books/{bookId}/cover.webp", true, 0);

        var response = await client.GetAsync("/api/books?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var content = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var item = content.RootElement.GetProperty("items")[0];

        Assert.False(item.TryGetProperty("images", out _));
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
    public async Task DeleteBook_WithImages_CascadesImageMetadataDelete()
    {
        using var client = factory.CreateApiClient();
        var createdBook = await CreateBookAsync(client);
        await SeedBookImageAsync(Guid.NewGuid(), createdBook.Id, $"books/{createdBook.Id}/cover.webp", true, 0);
        await SeedBookImageAsync(Guid.NewGuid(), createdBook.Id, $"books/{createdBook.Id}/gallery/001.webp", false, 1);

        var deleteResponse = await client.DeleteAsync($"/api/books/{createdBook.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var imageCount = await dbContext.BookImages.CountAsync(image => image.BookId == createdBook.Id);

        Assert.Equal(0, imageCount);
    }

    [Fact]
    public async Task BookImage_WithValidValues_PersistsMetadata()
    {
        var bookId = await SeedBookAsync("Image Persistence Book", "Image Author", 2);
        var imageId = Guid.NewGuid();

        await SeedBookImageAsync(imageId, bookId, $"books/{bookId}/cover.webp", true, 0);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var image = await dbContext.BookImages
            .AsNoTracking()
            .SingleAsync(image => image.Id == imageId);

        Assert.Equal(bookId, image.BookId);
        Assert.Equal($"books/{bookId}/cover.webp", image.ObjectName);
        Assert.True(image.IsCover);
        Assert.Equal(0, image.SortOrder);
    }

    [Fact]
    public async Task BookImage_WithMultipleCoverImagesForBook_IsRejectedByDatabase()
    {
        var bookId = await SeedBookAsync("Unique Cover Book", "Image Author", 2);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();

        await dbContext.BookImages.AddRangeAsync(
            new BookImage(Guid.NewGuid(), bookId, $"books/{bookId}/cover-1.webp", true, 0),
            new BookImage(Guid.NewGuid(), bookId, $"books/{bookId}/cover-2.webp", true, 1));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task UpdateBook_WithValidRequest_ReturnsOkAndUpdatesBook()
    {
        using var client = factory.CreateApiClient();
        var createdBook = await CreateBookAsync(client);
        var request = new UpdateBookRequest(
            "Refactoring",
            "Martin Fowler",
            5,
            nameof(BookCategory.Science),
            "Improving the design of existing code.",
            "978-0201485677",
            "Addison-Wesley",
            1999);

        var response = await client.PutAsJsonAsync($"/api/books/{createdBook.Id}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedBook = await response.Content.ReadFromJsonAsync<BookResponse>();

        Assert.NotNull(updatedBook);
        Assert.Equal(createdBook.Id, updatedBook.Id);
        Assert.Equal(request.Name, updatedBook.Name);
        Assert.Equal(request.Author, updatedBook.Author);
        Assert.Equal(request.Stock, updatedBook.Stock);
        Assert.Equal(request.Category, updatedBook.Category);
        Assert.Equal(request.Description, updatedBook.Description);
        Assert.Equal(request.Isbn, updatedBook.Isbn);
        Assert.Equal(request.Publisher, updatedBook.Publisher);
        Assert.Equal(request.PublishedYear, updatedBook.PublishedYear);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var storedBook = await dbContext.Books
            .AsNoTracking()
            .SingleAsync(book => book.Id == createdBook.Id);

        Assert.Equal(request.Name, storedBook.Name);
        Assert.Equal(request.Author, storedBook.Author);
        Assert.Equal(request.Stock, storedBook.Stock);
        Assert.Equal(BookCategory.Science, storedBook.Category);
        Assert.Equal(request.Description, storedBook.Description);
        Assert.Equal(request.Isbn, storedBook.Isbn);
        Assert.Equal(request.Publisher, storedBook.Publisher);
        Assert.Equal(request.PublishedYear, storedBook.PublishedYear);
    }

    [Fact]
    public async Task CreateBook_WithInvalidDescriptionLength_ReturnsBadRequest()
    {
        using var client = factory.CreateApiClient();
        var request = new CreateBookRequest("Long Description", "Author", 1, Description: new string('x', 4001));

        var response = await client.PostAsJsonAsync("/api/books", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateBook_WithInvalidPublisherLength_ReturnsBadRequest()
    {
        using var client = factory.CreateApiClient();
        var request = new CreateBookRequest("Long Publisher", "Author", 1, Publisher: new string('x', 201));

        var response = await client.PostAsJsonAsync("/api/books", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateBook_WithInvalidIsbnLength_ReturnsBadRequest()
    {
        using var client = factory.CreateApiClient();
        var request = new CreateBookRequest("Long ISBN", "Author", 1, Isbn: new string('1', 33));

        var response = await client.PostAsJsonAsync("/api/books", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateBook_WithInvalidPublishedYear_ReturnsBadRequest(int publishedYear)
    {
        using var client = factory.CreateApiClient();
        var request = new CreateBookRequest("Invalid Year", "Author", 1, PublishedYear: publishedYear);

        var response = await client.PostAsJsonAsync("/api/books", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateBook_WithFuturePublishedYear_ReturnsBadRequest()
    {
        using var client = factory.CreateApiClient();
        var request = new CreateBookRequest("Future Year", "Author", 1, PublishedYear: DateTime.UtcNow.Year + 1);

        var response = await client.PostAsJsonAsync("/api/books", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
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
    public async Task CreateBook_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = factory.CreateUnauthenticatedApiClient();
        var request = new CreateBookRequest("Clean Code", "Robert C. Martin", 3);

        var response = await client.PostAsJsonAsync("/api/books", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBook_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var authenticatedClient = factory.CreateApiClient();
        var createdBook = await CreateBookAsync(authenticatedClient);
        using var anonymousClient = factory.CreateUnauthenticatedApiClient();

        var response = await anonymousClient.DeleteAsync($"/api/books/{createdBook.Id}");

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

    private static async Task<BookResponse> CreateBookAsync(
        HttpClient client,
        CreateBookRequest? request = null)
    {
        request ??= new CreateBookRequest("Clean Code", "Robert C. Martin", 3);
        var response = await client.PostAsJsonAsync("/api/books", request);

        response.EnsureSuccessStatusCode();

        var book = await response.Content.ReadFromJsonAsync<BookResponse>();

        return book ?? throw new InvalidOperationException("Create book response body was empty.");
    }

    private async Task SeedBooksAsync(int count)
    {
        for (var index = 1; index <= count; index++)
        {
            await SeedBookAsync($"Book {index:000}", $"Author {index:000}", index % 5);
        }
    }

    private async Task<Guid> SeedBookAsync(
        string name,
        string author,
        int stock,
        BookCategory category = BookCategory.Novel)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var book = new Book(Guid.NewGuid(), name, author, stock, category);

        await dbContext.Books.AddAsync(book);
        await dbContext.SaveChangesAsync();

        return book.Id;
    }

    private async Task SeedBookImageAsync(
        Guid id,
        Guid bookId,
        string objectName,
        bool isCover,
        int sortOrder)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var image = new BookImage(id, bookId, objectName, isCover, sortOrder);

        await dbContext.BookImages.AddAsync(image);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<PagedBooksResponse> ReadPagedBooksResponseAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<PagedBooksResponse>()
            ?? throw new InvalidOperationException("Paged books response body was empty.");
    }

    private static void AssertBookNames(PagedBooksResponse page, IReadOnlyList<string> expectedNames)
    {
        Assert.Equal(expectedNames, page.Items.Select(book => book.Name).ToList());
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

    private sealed record CreateBookRequest(
        string Name,
        string Author,
        int Stock,
        string Category = nameof(BookCategory.Novel),
        string? Description = null,
        string? Isbn = null,
        string? Publisher = null,
        int? PublishedYear = null);

    private sealed record UpdateBookRequest(
        string Name,
        string Author,
        int Stock,
        string Category = nameof(BookCategory.Novel),
        string? Description = null,
        string? Isbn = null,
        string? Publisher = null,
        int? PublishedYear = null);

    private sealed record BookResponse(
        Guid Id,
        string Name,
        string Author,
        int Stock,
        string Category,
        string? Description,
        string? Isbn,
        string? Publisher,
        int? PublishedYear);

    private sealed record BookDetailResponse(
        Guid Id,
        string Name,
        string Author,
        int Stock,
        string Category,
        string? Description,
        string? Isbn,
        string? Publisher,
        int? PublishedYear,
        IReadOnlyList<BookImageResponse> Images);

    private sealed record BookImageResponse(
        Guid Id,
        string ObjectName,
        bool IsCover,
        int SortOrder);

    private sealed record PagedBooksResponse(
        IReadOnlyList<BookResponse> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    private sealed record RegisterRequest(string Username, string Email, string Password);

    private sealed record LoginRequest(string Username, string Password);

    private sealed record AuthResponse(string AccessToken, int ExpiresIn, string TokenType);

    private sealed record UserCredentials(string Username, string Email, string Password);
}
