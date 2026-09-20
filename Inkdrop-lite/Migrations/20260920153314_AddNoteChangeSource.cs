using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inkdrop_lite.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteChangeSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedSource",
                table: "Notes",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "app");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedSource",
                table: "Notes",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "app");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedSource",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "UpdatedSource",
                table: "Notes");
        }
    }
}
