using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rentier.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _0016_ReportProcessingAttempts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LastProcessingError",
            table: "Reports",
            type: "TEXT",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ProcessingAttempts",
            table: "Reports",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastProcessingError",
            table: "Reports");

        migrationBuilder.DropColumn(
            name: "ProcessingAttempts",
            table: "Reports");
    }
}
