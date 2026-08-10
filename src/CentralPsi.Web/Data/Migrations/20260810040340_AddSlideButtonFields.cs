using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralPsi.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSlideButtonFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ButtonText",
                table: "SlideImages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ButtonUrl",
                table: "SlideImages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ButtonText",
                table: "SlideImages");

            migrationBuilder.DropColumn(
                name: "ButtonUrl",
                table: "SlideImages");
        }
    }
}
