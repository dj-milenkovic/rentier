using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rentier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0009_FilingPaymentReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "Filings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "Filings");
        }
    }
}
