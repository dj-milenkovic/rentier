using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rentier.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0005_ImporterConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Importers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReportType = table.Column<int>(type: "INTEGER", nullable: false),
                    TaxpayerProfileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MailboxId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FromFilter = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: ""),
                    SubjectFilter = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: ""),
                    AttachmentRegex = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false, defaultValue: ""),
                    PaymentNotes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Importers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Importers_Mailboxes_MailboxId",
                        column: x => x.MailboxId,
                        principalTable: "Mailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Importers_TaxpayerProfiles_TaxpayerProfileId",
                        column: x => x.TaxpayerProfileId,
                        principalTable: "TaxpayerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Importers_MailboxId",
                table: "Importers",
                column: "MailboxId");

            migrationBuilder.CreateIndex(
                name: "IX_Importers_TaxpayerProfileId",
                table: "Importers",
                column: "TaxpayerProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Importers");
        }
    }
}
