using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruitment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameCandidateChildTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_candidate_document_candidates_candidate_id",
                table: "candidate_document");

            migrationBuilder.DropForeignKey(
                name: "fk_candidate_document_users_uploaded_by_user_id",
                table: "candidate_document");

            migrationBuilder.DropForeignKey(
                name: "fk_screening_candidates_candidate_id",
                table: "screening");

            migrationBuilder.DropForeignKey(
                name: "fk_screening_users_interviewer_user_id",
                table: "screening");

            migrationBuilder.DropPrimaryKey(
                name: "pk_screening",
                table: "screening");

            migrationBuilder.DropPrimaryKey(
                name: "pk_candidate_document",
                table: "candidate_document");

            migrationBuilder.RenameTable(
                name: "screening",
                newName: "screenings");

            migrationBuilder.RenameTable(
                name: "candidate_document",
                newName: "candidate_documents");

            migrationBuilder.RenameIndex(
                name: "ix_screening_interviewer_user_id",
                table: "screenings",
                newName: "ix_screenings_interviewer_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_screening_candidate_id",
                table: "screenings",
                newName: "ix_screenings_candidate_id");

            migrationBuilder.RenameIndex(
                name: "ix_candidate_document_uploaded_by_user_id",
                table: "candidate_documents",
                newName: "ix_candidate_documents_uploaded_by_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_candidate_document_candidate_id",
                table: "candidate_documents",
                newName: "ix_candidate_documents_candidate_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_screenings",
                table: "screenings",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_candidate_documents",
                table: "candidate_documents",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_candidate_documents_candidates_candidate_id",
                table: "candidate_documents",
                column: "candidate_id",
                principalTable: "candidates",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_candidate_documents_users_uploaded_by_user_id",
                table: "candidate_documents",
                column: "uploaded_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_screenings_candidates_candidate_id",
                table: "screenings",
                column: "candidate_id",
                principalTable: "candidates",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_screenings_users_interviewer_user_id",
                table: "screenings",
                column: "interviewer_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_candidate_documents_candidates_candidate_id",
                table: "candidate_documents");

            migrationBuilder.DropForeignKey(
                name: "fk_candidate_documents_users_uploaded_by_user_id",
                table: "candidate_documents");

            migrationBuilder.DropForeignKey(
                name: "fk_screenings_candidates_candidate_id",
                table: "screenings");

            migrationBuilder.DropForeignKey(
                name: "fk_screenings_users_interviewer_user_id",
                table: "screenings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_screenings",
                table: "screenings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_candidate_documents",
                table: "candidate_documents");

            migrationBuilder.RenameTable(
                name: "screenings",
                newName: "screening");

            migrationBuilder.RenameTable(
                name: "candidate_documents",
                newName: "candidate_document");

            migrationBuilder.RenameIndex(
                name: "ix_screenings_interviewer_user_id",
                table: "screening",
                newName: "ix_screening_interviewer_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_screenings_candidate_id",
                table: "screening",
                newName: "ix_screening_candidate_id");

            migrationBuilder.RenameIndex(
                name: "ix_candidate_documents_uploaded_by_user_id",
                table: "candidate_document",
                newName: "ix_candidate_document_uploaded_by_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_candidate_documents_candidate_id",
                table: "candidate_document",
                newName: "ix_candidate_document_candidate_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_screening",
                table: "screening",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_candidate_document",
                table: "candidate_document",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_candidate_document_candidates_candidate_id",
                table: "candidate_document",
                column: "candidate_id",
                principalTable: "candidates",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_candidate_document_users_uploaded_by_user_id",
                table: "candidate_document",
                column: "uploaded_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_screening_candidates_candidate_id",
                table: "screening",
                column: "candidate_id",
                principalTable: "candidates",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_screening_users_interviewer_user_id",
                table: "screening",
                column: "interviewer_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
