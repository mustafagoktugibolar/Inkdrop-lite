using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inkdrop_lite.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteNotebookNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Notebooks_NotebookId",
                table: "Notes");

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Notebooks_NotebookId",
                table: "Notes",
                column: "NotebookId",
                principalTable: "Notebooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Notebooks_NotebookId",
                table: "Notes");

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Notebooks_NotebookId",
                table: "Notes",
                column: "NotebookId",
                principalTable: "Notebooks",
                principalColumn: "Id");
        }
    }
}
