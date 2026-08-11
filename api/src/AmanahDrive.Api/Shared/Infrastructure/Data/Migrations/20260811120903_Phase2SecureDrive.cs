using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmanahDrive.Api.Shared.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2SecureDrive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "folders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ParentFolderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_folders_admin_users_UserId",
                        column: x => x.UserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_folders_folders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "file_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_file_items_admin_users_UserId",
                        column: x => x.UserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_file_items_folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_file_items_FolderId",
                table: "file_items",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_file_items_StorageKey",
                table: "file_items",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_items_UserId",
                table: "file_items",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_file_items_UserId_FolderId_OriginalFileName",
                table: "file_items",
                columns: new[] { "UserId", "FolderId", "OriginalFileName" },
                unique: true,
                filter: "\"FolderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_file_items_UserId_OriginalFileName",
                table: "file_items",
                columns: new[] { "UserId", "OriginalFileName" },
                unique: true,
                filter: "\"FolderId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_folders_ParentFolderId",
                table: "folders",
                column: "ParentFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_folders_UserId",
                table: "folders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_folders_UserId_Name",
                table: "folders",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"ParentFolderId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_folders_UserId_ParentFolderId_Name",
                table: "folders",
                columns: new[] { "UserId", "ParentFolderId", "Name" },
                unique: true,
                filter: "\"ParentFolderId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_items");

            migrationBuilder.DropTable(
                name: "folders");
        }
    }
}
