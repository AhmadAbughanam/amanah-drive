using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmanahDrive.Api.Shared.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentConversationContinuity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "agent_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE agent_runs
                SET "ConversationId" = "Id";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                table: "agent_runs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_UserId_ConversationId_CreatedAt",
                table: "agent_runs",
                columns: new[] { "UserId", "ConversationId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agent_runs_UserId_ConversationId_CreatedAt",
                table: "agent_runs");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "agent_runs");
        }
    }
}
