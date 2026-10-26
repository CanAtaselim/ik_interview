using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Recruitment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    normalized_phone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    linked_in_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    git_hub_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    in_talent_pool = table.Column<bool>(type: "boolean", nullable: false),
                    duplicate_acknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('simple', search_text)", stored: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
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
                    table.PrimaryKey("pk_candidates", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidates_dictionary_items_source_id",
                        column: x => x.source_id,
                        principalTable: "dictionary_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    extracted_text = table.Column<string>(type: "text", nullable: true),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_candidate_document", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidate_document_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_candidate_document_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "screening",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    interviewer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    interviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    soft_skills = table.Column<string>(type: "text", nullable: true),
                    technical_skills = table.Column<string>(type: "text", nullable: true),
                    experience_summary = table.Column<string>(type: "text", nullable: true),
                    motivation = table.Column<string>(type: "text", nullable: true),
                    total_experience_years = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    field_experience_years = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    notice_period_weeks = table.Column<int>(type: "integer", nullable: true),
                    hr_comment = table.Column<string>(type: "text", nullable: true),
                    current_benefits = table.Column<string>(type: "text", nullable: true),
                    work_model = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    open_to_relocation = table.Column<bool>(type: "boolean", nullable: true),
                    other_processes = table.Column<string>(type: "text", nullable: true),
                    military_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    overall_rating = table.Column<int>(type: "integer", nullable: true),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technical_skill_tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    current_salary_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    current_salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    current_salary_is_net = table.Column<bool>(type: "boolean", nullable: true),
                    expected_salary_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    expected_salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    expected_salary_is_net = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    educations = table.Column<string>(type: "jsonb", nullable: true),
                    languages = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_screening", x => x.id);
                    table.ForeignKey(
                        name: "fk_screening_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_screening_users_interviewer_user_id",
                        column: x => x.interviewer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "timeline_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_timeline_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_timeline_entries_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_logs_candidate_id_occurred_at",
                table: "access_logs",
                columns: new[] { "candidate_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_access_logs_user_id",
                table: "access_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_document_candidate_id",
                table: "candidate_document",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_document_uploaded_by_user_id",
                table: "candidate_document",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_created_at",
                table: "candidates",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_normalized_email",
                table: "candidates",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_normalized_phone",
                table: "candidates",
                column: "normalized_phone");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_search_vector",
                table: "candidates",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_source_id",
                table: "candidates",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_screening_candidate_id",
                table: "screening",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_screening_interviewer_user_id",
                table: "screening",
                column: "interviewer_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_timeline_entries_candidate_id_occurred_at",
                table: "timeline_entries",
                columns: new[] { "candidate_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_logs");

            migrationBuilder.DropTable(
                name: "candidate_document");

            migrationBuilder.DropTable(
                name: "screening");

            migrationBuilder.DropTable(
                name: "timeline_entries");

            migrationBuilder.DropTable(
                name: "candidates");
        }
    }
}
