using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Borrowing.Infrastructure;
using LibrarySystem.Modules.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Api.Configuration;

internal static class DatabaseMigrationExtensions
{
    public static async Task MigrateDatabasesAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        await serviceProvider.GetRequiredService<IdentityDbContext>()
            .Database
            .MigrateAsync(cancellationToken);

        await serviceProvider.GetRequiredService<BooksDbContext>()
            .Database
            .MigrateAsync(cancellationToken);

        await serviceProvider.GetRequiredService<BorrowingDbContext>()
            .Database
            .MigrateAsync(cancellationToken);
    }
}
