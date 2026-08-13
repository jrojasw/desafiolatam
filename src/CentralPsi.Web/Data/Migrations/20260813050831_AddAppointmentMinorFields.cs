using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralPsi.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentMinorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GuardianConsentAcceptedAtUtc",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianRelationship",
                table: "Appointments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForMinor",
                table: "Appointments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinorAge",
                table: "Appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinorFullName",
                table: "Appointments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuardianConsentAcceptedAtUtc",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "GuardianRelationship",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "IsForMinor",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "MinorAge",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "MinorFullName",
                table: "Appointments");
        }
    }
}
