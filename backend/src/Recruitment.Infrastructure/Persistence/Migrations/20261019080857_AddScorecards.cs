using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruitment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScorecards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "debrief_at",
                table: "interviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "debrief_by_user_id",
                table: "interviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "debrief_decision",
                table: "interviews",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "debrief_reason",
                table: "interviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bypass_reason",
                table: "application_stage_changes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "scorecards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    interview_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interviewer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recommendation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    general_note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reopen_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_scorecards", x => x.id);
                    table.ForeignKey(
                        name: "fk_scorecards_interviews_interview_id",
                        column: x => x.interview_id,
                        principalTable: "interviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_scorecards_users_interviewer_user_id",
                        column: x => x.interviewer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scorecard_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    question_version = table.Column<int>(type: "integer", nullable: true),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    guidance = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: true),
                    weight = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    scorecard_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scorecard_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_scorecard_items_dictionary_items_competency_id",
                        column: x => x.competency_id,
                        principalTable: "dictionary_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_scorecard_items_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_scorecard_items_scorecards_scorecard_id",
                        column: x => x.scorecard_id,
                        principalTable: "scorecards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_interviews_debrief_by_user_id",
                table: "interviews",
                column: "debrief_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_scorecard_items_competency_id",
                table: "scorecard_items",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "ix_scorecard_items_question_id",
                table: "scorecard_items",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_scorecard_items_scorecard_id",
                table: "scorecard_items",
                column: "scorecard_id");

            migrationBuilder.CreateIndex(
                name: "ix_scorecards_interview_id_interviewer_user_id",
                table: "scorecards",
                columns: new[] { "interview_id", "interviewer_user_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_scorecards_interviewer_user_id",
                table: "scorecards",
                column: "interviewer_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_interviews_users_debrief_by_user_id",
                table: "interviews",
                column: "debrief_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_interviews_users_debrief_by_user_id",
                table: "interviews");

            migrationBuilder.DropTable(
                name: "scorecard_items");

            migrationBuilder.DropTable(
                name: "scorecards");

            migrationBuilder.DropIndex(
                name: "ix_interviews_debrief_by_user_id",
                table: "interviews");

            migrationBuilder.DropColumn(
                name: "debrief_at",
                table: "interviews");

            migrationBuilder.DropColumn(
                name: "debrief_by_user_id",
                table: "interviews");

            migrationBuilder.DropColumn(
                name: "debrief_decision",
                table: "interviews");

            migrationBuilder.DropColumn(
                name: "debrief_reason",
                table: "interviews");

            migrationBuilder.DropColumn(
                name: "bypass_reason",
                table: "application_stage_changes");
        }
    }
}
