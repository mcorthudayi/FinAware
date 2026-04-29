using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinAware.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "Transactions",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalAmount",
                table: "Transactions",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OriginalCurrency",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "OriginalAmount",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "OriginalCurrency",
                table: "Transactions");
        }
    }
}
