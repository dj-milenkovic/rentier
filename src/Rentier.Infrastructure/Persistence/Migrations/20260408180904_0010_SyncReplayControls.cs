using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rentier.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _0010_SyncReplayControls : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Preserve InitialSyncDate → Cursor_LastSyncDate for mailboxes that haven't synced yet
        migrationBuilder.Sql(@"
                UPDATE Mailboxes
                SET Cursor_LastSyncDate = InitialSyncDate
                WHERE Cursor_LastSyncDate IS NULL AND InitialSyncDate IS NOT NULL;
            ");

        migrationBuilder.DropColumn(
            name: "InitialSyncDate",
            table: "Mailboxes");

        migrationBuilder.AddColumn<Guid>(
            name: "OriginalReportId",
            table: "Reports",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Reports_OriginalReportId",
            table: "Reports",
            column: "OriginalReportId");

        migrationBuilder.AddForeignKey(
            name: "FK_Reports_Reports_OriginalReportId",
            table: "Reports",
            column: "OriginalReportId",
            principalTable: "Reports",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Reports_Reports_OriginalReportId",
            table: "Reports");

        migrationBuilder.DropIndex(
            name: "IX_Reports_OriginalReportId",
            table: "Reports");

        migrationBuilder.DropColumn(
            name: "OriginalReportId",
            table: "Reports");

        migrationBuilder.AddColumn<DateOnly>(
            name: "InitialSyncDate",
            table: "Mailboxes",
            type: "TEXT",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1));
    }
}
