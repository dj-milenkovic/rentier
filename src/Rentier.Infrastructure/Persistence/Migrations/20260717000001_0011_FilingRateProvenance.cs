using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rentier.Infrastructure.Persistence.Migrations;

public partial class _0011_FilingRateProvenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ExchangeRateSourceDate",
            table: "Filings",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ExchangeRateSourceType",
            table: "Filings",
            type: "INTEGER",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ExchangeRateSourceDate", table: "Filings");
        migrationBuilder.DropColumn(name: "ExchangeRateSourceType", table: "Filings");
    }
}
