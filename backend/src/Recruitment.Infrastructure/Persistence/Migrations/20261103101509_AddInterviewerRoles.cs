using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruitment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewerRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "target_role",
                table: "interview_templates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Technical");

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "interview_interviewers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Technical");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "target_role",
                table: "interview_templates");

            migrationBuilder.DropColumn(
                name: "role",
                table: "interview_interviewers");
        }
    }
}
