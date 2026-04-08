using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rentier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0002_TaxpayerProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaxpayerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Jmbg = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OpstinaCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxpayerProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxpayerProfiles_Jmbg",
                table: "TaxpayerProfiles",
                column: "Jmbg",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxpayerProfiles");
        }
    }
}
