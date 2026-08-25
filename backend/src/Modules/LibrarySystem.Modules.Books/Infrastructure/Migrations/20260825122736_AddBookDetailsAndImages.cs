using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystem.Modules.Books.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookDetailsAndImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "books",
                table: "books",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "isbn",
                schema: "books",
                table: "books",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "published_year",
                schema: "books",
                table: "books",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "publisher",
                schema: "books",
                table: "books",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "book_images",
                schema: "books",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    is_cover = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_images", x => x.id);
                    table.CheckConstraint("ck_book_images_sort_order_non_negative", "\"sort_order\" >= 0");
                    table.ForeignKey(
                        name: "FK_book_images_books_book_id",
                        column: x => x.book_id,
                        principalSchema: "books",
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_book_images_book_id_sort_order_id",
                schema: "books",
                table: "book_images",
                columns: new[] { "book_id", "sort_order", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_book_images_book_id_cover",
                schema: "books",
                table: "book_images",
                column: "book_id",
                unique: true,
                filter: "\"is_cover\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_images",
                schema: "books");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "books",
                table: "books");

            migrationBuilder.DropColumn(
                name: "isbn",
                schema: "books",
                table: "books");

            migrationBuilder.DropColumn(
                name: "published_year",
                schema: "books",
                table: "books");

            migrationBuilder.DropColumn(
                name: "publisher",
                schema: "books",
                table: "books");
        }
    }
}
