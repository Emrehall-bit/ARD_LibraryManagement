using LibrarySystem.Api.IntegrationTests.Infrastructure;
using LibrarySystem.Modules.Books.Domain;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Books.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibrarySystem.Api.IntegrationTests.Books;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class BooksSeedTests(LibrarySystemApiFactory factory) : IAsyncLifetime
{
    private const int ExpectedSeedCount = BooksSeedExtensions.DevelopmentSeedCount;

    public async Task InitializeAsync()
    {
        await factory.ResetDataAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SeedBooksAsync_WhenDatabaseIsEmpty_AddsBibliographicBooksWithValidMetadata()
    {
        await factory.Services.SeedBooksAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var books = await dbContext.Books
            .AsNoTracking()
            .ToListAsync();
        var duplicateNameAuthorCount = books
            .GroupBy(book => new { book.Name, book.Author })
            .Count(group => group.Count() > 1);

        Assert.Equal(ExpectedSeedCount, books.Count);
        Assert.All(books, book =>
        {
            Assert.False(string.IsNullOrWhiteSpace(book.Name));
            Assert.False(string.IsNullOrWhiteSpace(book.Author));
            Assert.True(book.Name.Length <= 200);
            Assert.True(book.Author.Length <= 200);
            Assert.InRange(book.Stock, 0, 20);
            Assert.True(Enum.IsDefined(book.Category));
        });
        Assert.Equal(0, duplicateNameAuthorCount);
        Assert.Contains(books, book => book.Stock == 0);
        Assert.Contains(books, book => book.Category != BookCategory.Other);
    }

    [Fact]
    public async Task SeedBooksAsync_WhenRunRepeatedly_DoesNotAddDuplicates()
    {
        await factory.Services.SeedBooksAsync();
        await factory.Services.SeedBooksAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var bookCount = await dbContext.Books.CountAsync();

        Assert.Equal(ExpectedSeedCount, bookCount);
    }

    [Fact]
    public async Task SeedBooksAsync_WhenDatabaseHasBooks_DoesNotSeed()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
            var existingBook = new Book(Guid.NewGuid(), "Existing Book", "Integration Test", 1);

            await dbContext.Books.AddAsync(existingBook);
            await dbContext.SaveChangesAsync();
        }

        await factory.Services.SeedBooksAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
            var bookCount = await dbContext.Books.CountAsync();

            Assert.Equal(1, bookCount);
        }
    }
}
