using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inkdrop_lite.Migrations
{
    /// <inheritdoc />
    public partial class UserOwnershipSoftDeleteAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_Name",
                table: "Tags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NoteTags",
                table: "NoteTags");

            migrationBuilder.DropIndex(
                name: "IX_Notebooks_Name",
                table: "Notebooks");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Tags",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Tags",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Tags",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Tags",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Tags",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "NoteTags",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "NoteTags",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "NoteTags",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "NoteTags",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "NoteTags",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "NoteTags",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Notes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Notes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Notes",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Notebooks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Notebooks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Notebooks",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Notebooks",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_NoteTags",
                table: "NoteTags",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_OwnerId_Name",
                table: "Tags",
                columns: new[] { "OwnerId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NoteTags_NoteId",
                table: "NoteTags",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteTags_OwnerId_IsDeleted",
                table: "NoteTags",
                columns: new[] { "OwnerId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteTags_OwnerId_NoteId_TagId",
                table: "NoteTags",
                columns: new[] { "OwnerId", "NoteId", "TagId" },
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_OwnerId_IsDeleted_UpdatedAt",
                table: "Notes",
                columns: new[] { "OwnerId", "IsDeleted", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notebooks_OwnerId_Name",
                table: "Notebooks",
                columns: new[] { "OwnerId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_OwnerId_Name",
                table: "Tags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NoteTags",
                table: "NoteTags");

            migrationBuilder.DropIndex(
                name: "IX_NoteTags_NoteId",
                table: "NoteTags");

            migrationBuilder.DropIndex(
                name: "IX_NoteTags_OwnerId_IsDeleted",
                table: "NoteTags");

            migrationBuilder.DropIndex(
                name: "IX_NoteTags_OwnerId_NoteId_TagId",
                table: "NoteTags");

            migrationBuilder.DropIndex(
                name: "IX_Notes_OwnerId_IsDeleted_UpdatedAt",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Notebooks_OwnerId_Name",
                table: "Notebooks");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "NoteTags");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "NoteTags");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "NoteTags");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "NoteTags");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "NoteTags");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "NoteTags");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Notebooks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Notebooks");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Notebooks");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Notebooks");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NoteTags",
                table: "NoteTags",
                columns: new[] { "NoteId", "TagId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notebooks_Name",
                table: "Notebooks",
                column: "Name",
                unique: true);
        }
    }
}
