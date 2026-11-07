using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralPsi.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessionalTargetAudiences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetAudiences",
                table: "Professionals",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetAudiences",
                table: "Professionals");
        }
    }
}
