using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rentier.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _0004_MailboxConfiguration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Mailboxes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Host = table.Column<string>(type: "TEXT", maxLength: 253, nullable: false),
                Port = table.Column<int>(type: "INTEGER", nullable: false),
                Username = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                Cursor_LastSyncDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                Cursor_LastUid = table.Column<long>(type: "INTEGER", nullable: true),
                InitialSyncDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Mailboxes", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Mailboxes");
    }
}
