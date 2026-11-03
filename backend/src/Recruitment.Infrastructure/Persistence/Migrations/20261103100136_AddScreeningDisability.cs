using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruitment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScreeningDisability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "disability_description",
                table: "screenings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_disability",
                table: "screenings",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "disability_description",
                table: "screenings");

            migrationBuilder.DropColumn(
                name: "has_disability",
                table: "screenings");
        }
    }
}
