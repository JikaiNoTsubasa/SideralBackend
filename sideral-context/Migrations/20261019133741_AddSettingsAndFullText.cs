using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sideral_context.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsAndFullText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "full_text_attempts",
                table: "documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "full_text_error",
                table: "documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "full_text_indexed_on",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "full_text_next_attempt_on",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "full_text_status",
                table: "documents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PENDING");

            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_settings", x => x.key);
                    table.ForeignKey(
                        name: "fk_app_settings_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documents_full_text_status_full_text_next_attempt_on",
                table: "documents",
                columns: new[] { "full_text_status", "full_text_next_attempt_on" });

            migrationBuilder.CreateIndex(
                name: "ix_app_settings_updated_by_id",
                table: "app_settings",
                column: "updated_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropIndex(
                name: "ix_documents_full_text_status_full_text_next_attempt_on",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "full_text_attempts",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "full_text_error",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "full_text_indexed_on",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "full_text_next_attempt_on",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "full_text_status",
                table: "documents");
        }
    }
}
