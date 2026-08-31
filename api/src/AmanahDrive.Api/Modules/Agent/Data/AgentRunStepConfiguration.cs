using AmanahDrive.Api.Modules.Agent.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmanahDrive.Api.Modules.Agent.Data;

public sealed class AgentRunStepConfiguration : IEntityTypeConfiguration<AgentRunStep>
{
    public void Configure(EntityTypeBuilder<AgentRunStep> entity)
    {
        entity.ToTable("agent_run_steps");
        entity.HasKey(step => step.Id);
        entity.Property(step => step.Role).HasMaxLength(32).IsRequired();
        entity.Property(step => step.ToolCallId).HasMaxLength(128);
        entity.Property(step => step.ToolName).HasMaxLength(128);
        entity.Property(step => step.ToolArgumentsJson).HasColumnType("jsonb");
        entity.Property(step => step.ToolCallStatus).HasConversion<string>().HasMaxLength(32);
        entity.HasIndex(step => new { step.AgentRunId, step.Sequence }).IsUnique();
        entity.HasIndex(step => step.ToolCallId);
        entity.HasOne(step => step.AgentRun)
            .WithMany(run => run.Steps)
            .HasForeignKey(step => step.AgentRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
