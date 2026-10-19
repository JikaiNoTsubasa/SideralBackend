using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sideral_context.Migrations
{
    /// <inheritdoc />
    public partial class AddPostAnnouncement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_announcement",
                table: "posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_announcement",
                table: "posts");
        }
    }
}
