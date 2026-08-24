using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystem.Modules.Borrowing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBorrowRenewal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "renewal_count",
                schema: "borrowing",
                table: "borrow_records",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "renewal_count",
                schema: "borrowing",
                table: "borrow_records");
        }
    }
}
