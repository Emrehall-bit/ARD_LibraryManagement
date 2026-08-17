using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystem.Modules.Borrowing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialBorrowing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "borrowing");

            migrationBuilder.CreateTable(
                name: "borrow_records",
                schema: "borrowing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borrowed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    returned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_borrow_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_borrow_records_user_id_book_id_active",
                schema: "borrowing",
                table: "borrow_records",
                columns: new[] { "user_id", "book_id" },
                unique: true,
                filter: "\"returned_at\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "borrow_records",
                schema: "borrowing");
        }
    }
}
