using LibrarySystem.Modules.Books.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibrarySystem.Modules.Books.Infrastructure.Configurations;

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books", table =>
        {
            table.HasCheckConstraint("ck_books_stock_non_negative", "\"stock\" >= 0");
        });

        builder.HasKey(book => book.Id);

        builder.Property(book => book.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(book => book.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(book => book.Author)
            .HasColumnName("author")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(book => book.Stock)
            .HasColumnName("stock")
            .IsRequired();

        builder.Property(book => book.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(64)
            .HasDefaultValue(BookCategory.Other)
            .IsRequired();
    }
}
