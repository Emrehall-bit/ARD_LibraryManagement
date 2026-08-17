using LibrarySystem.Modules.Borrowing.Domain;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystem.Modules.Borrowing.Infrastructure;

public sealed class BorrowingDbContext(DbContextOptions<BorrowingDbContext> options) : DbContext(options)
{
    public DbSet<BorrowRecord> BorrowRecords => Set<BorrowRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("borrowing");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BorrowingDbContext).Assembly);
    }
}
