using LibrarySystem.Modules.Books.Domain;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Modules.Books.Infrastructure;

public sealed class BooksDbContext(DbContextOptions<BooksDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();

    public DbSet<BookImage> BookImages => Set<BookImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("books");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BooksDbContext).Assembly);
    }
}
