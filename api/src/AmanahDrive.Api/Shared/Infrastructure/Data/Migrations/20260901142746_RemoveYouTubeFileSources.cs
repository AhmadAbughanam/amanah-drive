using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmanahDrive.Api.Shared.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveYouTubeFileSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove the two failed, byte-less YouTube test rows before StorageKey becomes required again.
            // No uploaded file is affected; source-backed YouTube rows cannot satisfy the restored invariant.
            migrationBuilder.Sql("""
                DELETE FROM file_items
                WHERE "Source" = 'YouTube';
                """);

            migrationBuilder.DropColumn(
                name: "Source",
                table: "file_items");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "file_items");

            migrationBuilder.AlterColumn<string>(
                name: "StorageKey",
                table: "file_items",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StorageKey",
                table: "file_items",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "file_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Upload");

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "file_items",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }
    }
}
