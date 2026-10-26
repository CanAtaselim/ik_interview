using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recruitment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CandidateDocumentTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "type_id",
                table: "candidate_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidate_documents_type_id",
                table: "candidate_documents",
                column: "type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_candidate_documents_dictionary_items_type_id",
                table: "candidate_documents",
                column: "type_id",
                principalTable: "dictionary_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_candidate_documents_dictionary_items_type_id",
                table: "candidate_documents");

            migrationBuilder.DropIndex(
                name: "ix_candidate_documents_type_id",
                table: "candidate_documents");

            migrationBuilder.DropColumn(
                name: "type_id",
                table: "candidate_documents");
        }
    }
}
