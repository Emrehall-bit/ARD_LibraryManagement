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
    private const int ExpectedSeedCount = 5000;

    public async Task InitializeAsync()
    {
        await factory.ResetDataAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SeedBooksAsync_WhenDatabaseIsEmpty_AddsSyntheticBooks()
    {
        await factory.Services.SeedBooksAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();
        var bookCount = await dbContext.Books.CountAsync();
        var outOfStockCount = await dbContext.Books.CountAsync(book => book.Stock == 0);
        var negativeStockCount = await dbContext.Books.CountAsync(book => book.Stock < 0);

        Assert.Equal(ExpectedSeedCount, bookCount);
        Assert.True(outOfStockCount > 0);
        Assert.Equal(0, negativeStockCount);
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
