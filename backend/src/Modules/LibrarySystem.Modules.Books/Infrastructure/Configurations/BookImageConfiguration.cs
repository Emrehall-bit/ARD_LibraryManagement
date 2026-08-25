using LibrarySystem.Modules.Books.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibrarySystem.Modules.Books.Infrastructure.Configurations;

internal sealed class BookImageConfiguration : IEntityTypeConfiguration<BookImage>
{
    public void Configure(EntityTypeBuilder<BookImage> builder)
    {
        builder.ToTable("book_images", table =>
        {
            table.HasCheckConstraint("ck_book_images_sort_order_non_negative", "\"sort_order\" >= 0");
        });

        builder.HasKey(image => image.Id);

        builder.Property(image => image.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(image => image.BookId)
            .HasColumnName("book_id")
            .IsRequired();

        builder.Property(image => image.ObjectName)
            .HasColumnName("object_name")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(image => image.IsCover)
            .HasColumnName("is_cover")
            .IsRequired();

        builder.Property(image => image.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder
            .HasOne<Book>()
            .WithMany(book => book.Images)
            .HasForeignKey(image => image.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(image => new { image.BookId, image.SortOrder, image.Id })
            .HasDatabaseName("ix_book_images_book_id_sort_order_id");

        builder.HasIndex(image => image.BookId)
            .IsUnique()
            .HasFilter("\"is_cover\" = true")
            .HasDatabaseName("ux_book_images_book_id_cover");
    }
}
