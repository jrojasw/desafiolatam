using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CronogramaTrabajo.Web.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPapelera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Eliminada",
                table: "Tareas",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EliminadaPor",
                table: "Tareas",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "Tareas",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Eliminada",
                table: "Tareas");

            migrationBuilder.DropColumn(
                name: "EliminadaPor",
                table: "Tareas");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "Tareas");
        }
    }
}
