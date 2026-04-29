using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinAware.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingSaleSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RelatedDepositId",
                table: "SavingTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                table: "SavingTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelatedDepositId",
                table: "SavingTransactions");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "SavingTransactions");
        }
    }
}
