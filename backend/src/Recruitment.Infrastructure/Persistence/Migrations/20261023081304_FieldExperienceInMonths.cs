using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruitment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FieldExperienceInMonths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "field_experience_months", table: "screenings", type: "integer", nullable: true);

            // Mevcut kayıtlar kaybolmasın: yıl → ay.
            migrationBuilder.Sql(
                "update screenings set field_experience_months = round(field_experience_years * 12)::int " +
                "where field_experience_years is not null;");

            migrationBuilder.DropColumn(name: "field_experience_years", table: "screenings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "field_experience_years", table: "screenings", type: "numeric(4,1)", precision: 4, scale: 1, nullable: true);

            migrationBuilder.Sql(
                "update screenings set field_experience_years = round(field_experience_months / 12.0, 1) " +
                "where field_experience_months is not null;");

            migrationBuilder.DropColumn(name: "field_experience_months", table: "screenings");
        }
    }
}
