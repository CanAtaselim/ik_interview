using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruitment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineAndBlacklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AddColumn<string>(
                name: "normalized_name",
                table: "candidates",
                type: "character varying(201)",
                maxLength: 201,
                nullable: false,
                defaultValue: "");

            // Mevcut adaylar için sadeleştirilmiş adı doldurur (uygulamadaki ContactNormalizer.NormalizeName
            // ile aynı sonuç: küçük harf, i türevleri "i", harf/rakam dışı her şey tek boşluk).
            migrationBuilder.Sql(
                """
                update candidates
                set normalized_name = trim(regexp_replace(
                    replace(replace(lower(replace(replace(first_name || ' ' || last_name, 'İ', 'i'), 'I', 'i')), 'ı', 'i'), 'i̇', 'i'),
                    '[^[:alnum:]]+', ' ', 'g'));
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "candidate_id",
                table: "access_logs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "blacklist_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removal_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_blacklist_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_blacklist_entries_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_blacklist_entries_dictionary_items_reason_id",
                        column: x => x.reason_id,
                        principalTable: "dictionary_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_blacklist_entries_users_approved_by_user_id",
                        column: x => x.approved_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_blacklist_entries_users_removed_by_user_id",
                        column: x => x.removed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_blacklist_entries_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_pipeline_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_stage_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    is_optional = table.Column<bool>(type: "boolean", nullable: false),
                    pipeline_template_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_pipeline_stage_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_pipeline_stage_definitions_pipeline_templates_pipeline_temp",
                        column: x => x.pipeline_template_id,
                        principalTable: "pipeline_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    stage_entered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rejection_reason_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closing_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_applications", x => x.id);
                    table.ForeignKey(
                        name: "fk_applications_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_applications_dictionary_items_rejection_reason_id",
                        column: x => x.rejection_reason_id,
                        principalTable: "dictionary_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_applications_pipeline_stage_definition_stage_id",
                        column: x => x.stage_id,
                        principalTable: "pipeline_stage_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_applications_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "application_stage_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    previous_stage_entered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    undone_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_application_stage_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_application_stage_changes_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_stage_changes_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_application_stage_changes_application_id",
                table: "application_stage_changes",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_application_stage_changes_changed_by_user_id",
                table: "application_stage_changes",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_candidate_id_position_id",
                table: "applications",
                columns: new[] { "candidate_id", "position_id" },
                unique: true,
                filter: "status = 'Active' and is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_applications_position_id",
                table: "applications",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_rejection_reason_id",
                table: "applications",
                column: "rejection_reason_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_stage_id",
                table: "applications",
                column: "stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_blacklist_entries_approved_by_user_id",
                table: "blacklist_entries",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_blacklist_entries_candidate_id",
                table: "blacklist_entries",
                column: "candidate_id",
                unique: true,
                filter: "status in ('PendingApproval', 'Active') and is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_blacklist_entries_reason_id",
                table: "blacklist_entries",
                column: "reason_id");

            migrationBuilder.CreateIndex(
                name: "ix_blacklist_entries_removed_by_user_id",
                table: "blacklist_entries",
                column: "removed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_blacklist_entries_requested_by_user_id",
                table: "blacklist_entries",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_blacklist_entries_status",
                table: "blacklist_entries",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_stage_definitions_pipeline_template_id",
                table: "pipeline_stage_definitions",
                column: "pipeline_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_templates_is_default",
                table: "pipeline_templates",
                column: "is_default",
                unique: true,
                filter: "is_default = true and is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_stage_changes");

            migrationBuilder.DropTable(
                name: "blacklist_entries");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "pipeline_stage_definitions");

            migrationBuilder.DropTable(
                name: "pipeline_templates");

            migrationBuilder.DropColumn(
                name: "normalized_name",
                table: "candidates");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AlterColumn<Guid>(
                name: "candidate_id",
                table: "access_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
