using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralPsi.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentInboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImapUid = table.Column<string>(type: "text", nullable: false),
                    FromAddress = table.Column<string>(type: "text", nullable: false),
                    FromName = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    BodyPreview = table.Column<string>(type: "text", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reviewed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentInboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentInboxAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentInboxMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    StoredPath = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentInboxAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentInboxAttachments_PaymentInboxMessages_PaymentInboxMe~",
                        column: x => x.PaymentInboxMessageId,
                        principalTable: "PaymentInboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentInboxAttachments_PaymentInboxMessageId",
                table: "PaymentInboxAttachments",
                column: "PaymentInboxMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentInboxMessages_ImapUid",
                table: "PaymentInboxMessages",
                column: "ImapUid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentInboxAttachments");

            migrationBuilder.DropTable(
                name: "PaymentInboxMessages");
        }
    }
}
