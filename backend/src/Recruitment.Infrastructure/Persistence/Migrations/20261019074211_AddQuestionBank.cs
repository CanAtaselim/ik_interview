using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruitment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "interview_template_ids",
                table: "positions",
                type: "uuid[]",
                nullable: false,
                // Mevcut pozisyonlar şablonsuz başlar.
                defaultValueSql: "'{}'::uuid[]");

            migrationBuilder.AddColumn<Guid>(
                name: "template_id",
                table: "interviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "interview_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    interview_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_interview_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_interview_templates_dictionary_items_department_id",
                        column: x => x.department_id,
                        principalTable: "dictionary_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_interview_templates_dictionary_items_interview_type_id",
                        column: x => x.interview_type_id,
                        principalTable: "dictionary_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_interview_templates_dictionary_items_level_id",
                        column: x => x.level_id,
                        principalTable: "dictionary_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    difficulty = table.Column<int>(type: "integer", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    guidance = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_questions_dictionary_items_competency_id",
                        column: x => x.competency_id,
                        principalTable: "dictionary_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_questions_dictionary_items_department_id",
                        column: x => x.department_id,
                        principalTable: "dictionary_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_questions_dictionary_items_level_id",
                        column: x => x.level_id,
                        principalTable: "dictionary_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "interview_template_questions",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interview_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_interview_template_questions", x => new { x.interview_template_id, x.question_id });
                    table.ForeignKey(
                        name: "fk_interview_template_questions_interview_templates_interview_",
                        column: x => x.interview_template_id,
                        principalTable: "interview_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_interview_template_questions_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "question_versions",
                columns: table => new
                {
                    number = table.Column<int>(type: "integer", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    guidance = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    difficulty = table.Column<int>(type: "integer", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_versions", x => new { x.question_id, x.number });
                    table.ForeignKey(
                        name: "fk_question_versions_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_question_versions_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_interviews_template_id",
                table: "interviews",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_interview_template_questions_question_id",
                table: "interview_template_questions",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_interview_templates_department_id_is_active",
                table: "interview_templates",
                columns: new[] { "department_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_interview_templates_interview_type_id",
                table: "interview_templates",
                column: "interview_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_interview_templates_level_id",
                table: "interview_templates",
                column: "level_id");

            migrationBuilder.CreateIndex(
                name: "ix_question_versions_created_by_user_id",
                table: "question_versions",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_questions_competency_id",
                table: "questions",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_questions_department_id_is_active",
                table: "questions",
                columns: new[] { "department_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_level_id",
                table: "questions",
                column: "level_id");

            migrationBuilder.AddForeignKey(
                name: "fk_interviews_interview_templates_template_id",
                table: "interviews",
                column: "template_id",
                principalTable: "interview_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_interviews_interview_templates_template_id",
                table: "interviews");

            migrationBuilder.DropTable(
                name: "interview_template_questions");

            migrationBuilder.DropTable(
                name: "question_versions");

            migrationBuilder.DropTable(
                name: "interview_templates");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropIndex(
                name: "ix_interviews_template_id",
                table: "interviews");

            migrationBuilder.DropColumn(
                name: "interview_template_ids",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "template_id",
                table: "interviews");
        }
    }
}
