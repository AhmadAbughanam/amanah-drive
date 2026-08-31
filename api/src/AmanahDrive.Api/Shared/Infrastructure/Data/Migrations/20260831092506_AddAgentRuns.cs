using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmanahDrive.Api.Shared.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FinalAnswer = table.Column<string>(type: "text", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_runs_admin_users_UserId",
                        column: x => x.UserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_run_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    ToolCallId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ToolName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ToolArgumentsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ToolCallStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_run_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_run_steps_agent_runs_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "agent_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_run_steps_AgentRunId_Sequence",
                table: "agent_run_steps",
                columns: new[] { "AgentRunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_run_steps_ToolCallId",
                table: "agent_run_steps",
                column: "ToolCallId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_UserId",
                table: "agent_runs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_UserId_UpdatedAt",
                table: "agent_runs",
                columns: new[] { "UserId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_run_steps");

            migrationBuilder.DropTable(
                name: "agent_runs");
        }
    }
}
