using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rentier.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _0007_ReportEnrichment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Reports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ImportDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                ImporterId = table.Column<Guid>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                ReportName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                AttachmentContent = table.Column<byte[]>(type: "BLOB", nullable: true),
                MailboxMessageId = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Reports", x => x.Id);
                table.ForeignKey(
                    name: "FK_Reports_Importers_ImporterId",
                    column: x => x.ImporterId,
                    principalTable: "Importers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Filings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                TaxpayerProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                TaxPeriod = table.Column<DateOnly>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                IncomeType = table.Column<int>(type: "INTEGER", nullable: false),
                PayingEntity = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                IncomeDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                GrossIncomeRsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                WhtPaidRsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                GrossTaxPayableRsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                TaxPayableRsd = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                FilingDeadline = table.Column<DateOnly>(type: "TEXT", nullable: false),
                ReportId = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Filings", x => x.Id);
                table.ForeignKey(
                    name: "FK_Filings_Reports_ReportId",
                    column: x => x.ReportId,
                    principalTable: "Reports",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_Filings_TaxpayerProfiles_TaxpayerProfileId",
                    column: x => x.TaxpayerProfileId,
                    principalTable: "TaxpayerProfiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Filings_ReportId",
            table: "Filings",
            column: "ReportId");

        migrationBuilder.CreateIndex(
            name: "IX_Filings_TaxpayerProfileId",
            table: "Filings",
            column: "TaxpayerProfileId");

        migrationBuilder.CreateIndex(
            name: "IX_Reports_ImporterId",
            table: "Reports",
            column: "ImporterId");

        migrationBuilder.CreateIndex(
            name: "IX_Reports_ImporterId_ReportName",
            table: "Reports",
            columns: new[] { "ImporterId", "ReportName" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Filings");

        migrationBuilder.DropTable(
            name: "Reports");
    }
}
