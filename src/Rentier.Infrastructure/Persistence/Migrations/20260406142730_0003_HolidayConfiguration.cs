using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rentier.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _0003_HolidayConfiguration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HolidayYearRange",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                StartYear = table.Column<int>(type: "INTEGER", nullable: false),
                EndYear = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HolidayYearRange", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PublicHolidays",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Year = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PublicHolidays", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PublicHolidays_Year",
            table: "PublicHolidays",
            column: "Year");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "HolidayYearRange");

        migrationBuilder.DropTable(
            name: "PublicHolidays");
    }
}
