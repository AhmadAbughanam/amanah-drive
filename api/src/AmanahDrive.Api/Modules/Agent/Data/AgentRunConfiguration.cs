using AmanahDrive.Api.Modules.Agent.Models;
using AmanahDrive.Api.Modules.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmanahDrive.Api.Modules.Agent.Data;

public sealed class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> entity)
    {
        entity.ToTable("agent_runs");
        entity.HasKey(run => run.Id);
        entity.Property(run => run.Question).HasMaxLength(4000).IsRequired();
        entity.Property(run => run.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        entity.Property(run => run.FailureReason).HasMaxLength(512);
        entity.HasIndex(run => run.UserId);
        entity.HasIndex(run => new { run.UserId, run.UpdatedAt });
        entity.HasOne<AdminUser>()
            .WithMany()
            .HasForeignKey(run => run.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
