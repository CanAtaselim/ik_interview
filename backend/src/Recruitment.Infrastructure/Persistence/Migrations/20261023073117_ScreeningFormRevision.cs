using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruitment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScreeningFormRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "education_note", table: "screenings", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "languages_note", table: "screenings", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "location_note", table: "screenings", type: "text", nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "expected_salary_max", table: "screenings", type: "numeric(14,2)", precision: 14, scale: 2, nullable: true);
            migrationBuilder.AddColumn<bool>(name: "has_travel_restriction", table: "screenings", type: "boolean", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "location_district", table: "screenings", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "location_province", table: "screenings", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<int>(name: "total_experience_months", table: "screenings", type: "integer", nullable: true);

            // Mevcut kayıtlar kaybolmasın: yıl → ay; serbest metin lokasyon → lokasyon açıklaması.
            migrationBuilder.Sql(
                "update screenings set total_experience_months = round(total_experience_years * 12)::int " +
                "where total_experience_years is not null;");
            migrationBuilder.Sql("update screenings set location_note = location where location is not null;");

            // Formdan kaldırılan alanlar ("mevcut yan haklar", "devam eden diğer süreçler") ve eski kolonlar.
            migrationBuilder.DropColumn(name: "location", table: "screenings");
            migrationBuilder.DropColumn(name: "total_experience_years", table: "screenings");
            migrationBuilder.DropColumn(name: "current_benefits", table: "screenings");
            migrationBuilder.DropColumn(name: "other_processes", table: "screenings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "location", table: "screenings", type: "character varying(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "total_experience_years", table: "screenings", type: "numeric(4,1)", precision: 4, scale: 1, nullable: true);
            migrationBuilder.AddColumn<string>(name: "current_benefits", table: "screenings", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "other_processes", table: "screenings", type: "text", nullable: true);

            migrationBuilder.Sql(
                "update screenings set total_experience_years = round(total_experience_months / 12.0, 1) " +
                "where total_experience_months is not null;");
            migrationBuilder.Sql(
                "update screenings set location = left(coalesce(location_province || ' / ' || location_district, location_province, location_note), 200) " +
                "where location_province is not null or location_note is not null;");

            migrationBuilder.DropColumn(name: "education_note", table: "screenings");
            migrationBuilder.DropColumn(name: "languages_note", table: "screenings");
            migrationBuilder.DropColumn(name: "location_note", table: "screenings");
            migrationBuilder.DropColumn(name: "expected_salary_max", table: "screenings");
            migrationBuilder.DropColumn(name: "has_travel_restriction", table: "screenings");
            migrationBuilder.DropColumn(name: "location_district", table: "screenings");
            migrationBuilder.DropColumn(name: "location_province", table: "screenings");
            migrationBuilder.DropColumn(name: "total_experience_months", table: "screenings");
        }
    }
}
