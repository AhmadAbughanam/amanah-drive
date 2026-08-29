using AmanahDrive.Api.Modules.Admin.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmanahDrive.Api.Modules.Admin.Data;

public sealed class AiUsageRecordConfiguration : IEntityTypeConfiguration<AiUsageRecord>
{
    public void Configure(EntityTypeBuilder<AiUsageRecord> entity)
    {
        entity.ToTable("ai_usage_records");
        entity.HasKey(record => record.Id);
        entity.Property(record => record.Provider).HasMaxLength(100).IsRequired();
        entity.Property(record => record.Model).HasMaxLength(200);
        entity.Property(record => record.Operation).HasMaxLength(64).IsRequired();
        entity.Property(record => record.EstimatedCostUsd).HasPrecision(18, 8);
        entity.Property(record => record.ErrorType).HasMaxLength(200);
        entity.HasIndex(record => record.OccurredAt);
        entity.HasIndex(record => new { record.Provider, record.Model });
        entity.HasIndex(record => record.Operation);
    }
}
