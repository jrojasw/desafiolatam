using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralPsi.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessionalFonasaConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FonasaConfirmationSentAtUtc",
                table: "Professionals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FonasaConfirmationToken",
                table: "Professionals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FonasaConfirmedAtUtc",
                table: "Professionals",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FonasaConfirmationSentAtUtc",
                table: "Professionals");

            migrationBuilder.DropColumn(
                name: "FonasaConfirmationToken",
                table: "Professionals");

            migrationBuilder.DropColumn(
                name: "FonasaConfirmedAtUtc",
                table: "Professionals");
        }
    }
}
