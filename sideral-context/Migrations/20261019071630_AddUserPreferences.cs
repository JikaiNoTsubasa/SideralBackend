using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sideral_context.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "accent_color",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "indigo");

            migrationBuilder.AddColumn<string>(
                name: "theme",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "light");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accent_color",
                table: "users");

            migrationBuilder.DropColumn(
                name: "theme",
                table: "users");
        }
    }
}
