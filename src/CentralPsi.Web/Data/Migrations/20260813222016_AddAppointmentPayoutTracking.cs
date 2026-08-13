using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralPsi.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentPayoutTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProfessionalPaidAtUtc",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfessionalPaymentNote",
                table: "Appointments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfessionalPayoutAmount",
                table: "Appointments",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            // Backfill existing rows (created before payout tracking existed) with the standard payout so they
            // don't show up as $0 in the new admin Pagos page.
            migrationBuilder.Sql(@"UPDATE ""Appointments"" SET ""ProfessionalPayoutAmount"" = 15000 WHERE ""ProfessionalPayoutAmount"" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfessionalPaidAtUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ProfessionalPaymentNote",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ProfessionalPayoutAmount",
                table: "Appointments");
        }
    }
}
