using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystem.Modules.Borrowing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBorrowDueDate : Migration
    {
        private const int DefaultLoanPeriodDays = 14;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "due_date",
                schema: "borrowing",
                table: "borrow_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql($"""
                UPDATE borrowing.borrow_records
                SET due_date = borrowed_at + INTERVAL '{DefaultLoanPeriodDays} days'
                WHERE due_date IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "due_date",
                schema: "borrowing",
                table: "borrow_records",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "due_date",
                schema: "borrowing",
                table: "borrow_records");
        }
    }
}
