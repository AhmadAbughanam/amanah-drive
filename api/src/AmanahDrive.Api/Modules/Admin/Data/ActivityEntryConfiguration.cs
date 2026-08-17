using AmanahDrive.Api.Modules.Admin.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmanahDrive.Api.Modules.Admin.Data;

public sealed class ActivityEntryConfiguration : IEntityTypeConfiguration<ActivityEntry>
{
    public void Configure(EntityTypeBuilder<ActivityEntry> entity)
    {
        entity.ToTable("activity_entries");
        entity.HasKey(entry => entry.Id);
        entity.Property(entry => entry.Type).HasMaxLength(64).IsRequired();
        entity.Property(entry => entry.Summary).HasMaxLength(500).IsRequired();
        entity.HasIndex(entry => entry.OccurredAt);
        entity.HasIndex(entry => entry.Type);
    }
}
