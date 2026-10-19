using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sideral_context.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentFullTextIdentifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "full_text_identifier",
                table: "documents",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateIndex(
                name: "ix_documents_full_text_identifier",
                table: "documents",
                column: "full_text_identifier",
                unique: true);

            // Les documents déjà envoyés l'ont été sous leur id numérique : on les renvoie sous leur nouvel identifiant
            migrationBuilder.Sql(
                "UPDATE documents SET full_text_status = 'PENDING', full_text_attempts = 0, full_text_next_attempt_on = NULL, full_text_error = NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_documents_full_text_identifier",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "full_text_identifier",
                table: "documents");
        }
    }
}
