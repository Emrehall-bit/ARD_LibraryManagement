using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Modules.Borrowing.Infrastructure;

public sealed class BorrowingDbContext(DbContextOptions<BorrowingDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("borrowing");
    }
}
