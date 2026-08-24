using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystem.Modules.Books.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                schema: "books",
                table: "books",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Other");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "category",
                schema: "books",
                table: "books");
        }
    }
}
