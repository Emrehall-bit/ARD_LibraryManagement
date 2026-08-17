using LibrarySystem.Modules.Borrowing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibrarySystem.Modules.Borrowing.Infrastructure.Configurations;

internal sealed class BorrowRecordConfiguration : IEntityTypeConfiguration<BorrowRecord>
{
    public void Configure(EntityTypeBuilder<BorrowRecord> builder)
    {
        builder.ToTable("borrow_records");

        builder.HasKey(borrowRecord => borrowRecord.Id);

        builder.Property(borrowRecord => borrowRecord.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(borrowRecord => borrowRecord.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(borrowRecord => borrowRecord.BookId)
            .HasColumnName("book_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(borrowRecord => borrowRecord.BorrowedAt)
            .HasColumnName("borrowed_at")
            .IsRequired();

        builder.Property(borrowRecord => borrowRecord.ReturnedAt)
            .HasColumnName("returned_at");

        builder.HasIndex(borrowRecord => new { borrowRecord.UserId, borrowRecord.BookId })
            .IsUnique()
            .HasFilter("\"returned_at\" IS NULL")
            .HasDatabaseName("ux_borrow_records_user_id_book_id_active");
    }
}
