using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruitment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCvReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cv_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggestion_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    rejection_reason_id = table.Column<Guid>(type: "uuid", nullable: true),
                    suggested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    escalate_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reminder_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    escalated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cv_reviews", x => x.id);
                    table.ForeignKey(
                        name: "fk_cv_reviews_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cv_reviews_dictionary_items_department_id",
                        column: x => x.department_id,
                        principalTable: "dictionary_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cv_reviews_dictionary_items_rejection_reason_id",
                        column: x => x.rejection_reason_id,
                        principalTable: "dictionary_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cv_reviews_job_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cv_reviews_users_reviewer_user_id",
                        column: x => x.reviewer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cv_reviews_users_suggested_by_user_id",
                        column: x => x.suggested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "holidays",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    day = table.Column<int>(type: "integer", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_holidays", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cv_reviews_application_id",
                table: "cv_reviews",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_cv_reviews_candidate_id",
                table: "cv_reviews",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_cv_reviews_decision_due_at",
                table: "cv_reviews",
                columns: new[] { "decision", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_cv_reviews_department_id",
                table: "cv_reviews",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_cv_reviews_rejection_reason_id",
                table: "cv_reviews",
                column: "rejection_reason_id");

            migrationBuilder.CreateIndex(
                name: "ix_cv_reviews_reviewer_user_id_decision",
                table: "cv_reviews",
                columns: new[] { "reviewer_user_id", "decision" });

            migrationBuilder.CreateIndex(
                name: "ix_cv_reviews_suggested_by_user_id",
                table: "cv_reviews",
                column: "suggested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_holidays_month_day_year",
                table: "holidays",
                columns: new[] { "month", "day", "year" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cv_reviews");

            migrationBuilder.DropTable(
                name: "holidays");
        }
    }
}
