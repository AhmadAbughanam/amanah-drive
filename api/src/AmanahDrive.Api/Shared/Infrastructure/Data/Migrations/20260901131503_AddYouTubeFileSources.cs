using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmanahDrive.Api.Shared.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddYouTubeFileSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
    }
}
