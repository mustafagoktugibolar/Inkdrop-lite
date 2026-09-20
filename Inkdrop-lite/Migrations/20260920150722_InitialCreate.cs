using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inkdrop_lite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ContentLength = table.Column<long>(type: "INTEGER", nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.CheckConstraint("CK_Attachments_ContentLength", "\"ContentLength\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, collation: "NOCASE"),
                    Color = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notebooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ParentNotebookId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: true),
                    IconType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IconSvg = table.Column<string>(type: "TEXT", maxLength: 262144, nullable: true),
                    IconAttachmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notebooks", x => x.Id);
                    table.CheckConstraint("CK_Notebooks_NotOwnParent", "\"ParentNotebookId\" IS NULL OR \"ParentNotebookId\" <> \"Id\"");
                    table.ForeignKey(
                        name: "FK_Notebooks_Attachments_IconAttachmentId",
                        column: x => x.IconAttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notebooks_Notebooks_ParentNotebookId",
                        column: x => x.ParentNotebookId,
                        principalTable: "Notebooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 1048576, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Pinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotebookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceTemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                    table.CheckConstraint("CK_Notes_NotOwnTemplate", "\"SourceTemplateId\" IS NULL OR \"SourceTemplateId\" <> \"Id\"");
                    table.ForeignKey(
                        name: "FK_Notes_Notebooks_NotebookId",
                        column: x => x.NotebookId,
                        principalTable: "Notebooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notes_Notes_SourceTemplateId",
                        column: x => x.SourceTemplateId,
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NoteAttachments",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteAttachments", x => new { x.NoteId, x.AttachmentId });
                    table.ForeignKey(
                        name: "FK_NoteAttachments_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NoteAttachments_Notes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoteTags",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteTags", x => new { x.NoteId, x.TagId });
                    table.ForeignKey(
                        name: "FK_NoteTags_Notes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NoteTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_Hash",
                table: "Attachments",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_StoragePath",
                table: "Attachments",
                column: "StoragePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoteAttachments_AttachmentId",
                table: "NoteAttachments",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notebooks_IconAttachmentId",
                table: "Notebooks",
                column: "IconAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notebooks_Name",
                table: "Notebooks",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Notebooks_Order",
                table: "Notebooks",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_Notebooks_ParentNotebookId",
                table: "Notebooks",
                column: "ParentNotebookId");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_NotebookId",
                table: "Notes",
                column: "NotebookId");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_NotebookId_UpdatedAt",
                table: "Notes",
                columns: new[] { "NotebookId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_SourceTemplateId",
                table: "Notes",
                column: "SourceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_Status",
                table: "Notes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_UpdatedAt",
                table: "Notes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NoteTags_TagId",
                table: "NoteTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_OwnerId_Name",
                table: "Tags",
                columns: new[] { "OwnerId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_UpdatedAt",
                table: "Tags",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoteAttachments");

            migrationBuilder.DropTable(
                name: "NoteTags");

            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Notebooks");

            migrationBuilder.DropTable(
                name: "Attachments");
        }
    }
}
