using System.Reflection;
using System.Text.Json;
using LibrarySystem.Modules.Books.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibrarySystem.Modules.Books.Infrastructure.Seeding;

public static class BooksSeedExtensions
{
    private const string SeedResourceName =
        "LibrarySystem.Modules.Books.Infrastructure.Seeding.books.seed.json";

    public static async Task SeedBooksAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();

        if (await dbContext.Books.AnyAsync(cancellationToken))
        {
            return;
        }

        var seedBooks = await ReadSeedBooksAsync(cancellationToken);
        var books = seedBooks
            .Select(seedBook => new Book(Guid.NewGuid(), seedBook.Name, seedBook.Author, seedBook.Stock))
            .ToList();

        await dbContext.Books.AddRangeAsync(books, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<BookSeedEntry>> ReadSeedBooksAsync(
        CancellationToken cancellationToken)
    {
        var assembly = typeof(BooksSeedExtensions).Assembly;
        await using var stream = assembly.GetManifestResourceStream(SeedResourceName)
            ?? throw new InvalidOperationException($"Books seed resource '{SeedResourceName}' could not be found.");

        var seedBooks = await JsonSerializer.DeserializeAsync<List<BookSeedEntry>>(
            stream,
            JsonSerializerOptions.Web,
            cancellationToken);

        if (seedBooks is null || seedBooks.Count == 0)
        {
            throw new InvalidOperationException("Books seed resource did not contain any entries.");
        }

        return seedBooks;
    }

    private sealed record BookSeedEntry(string Name, string Author, int Stock);
}
