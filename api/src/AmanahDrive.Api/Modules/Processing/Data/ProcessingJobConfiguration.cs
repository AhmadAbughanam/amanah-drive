using AmanahDrive.Api.Modules.Processing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmanahDrive.Api.Modules.Processing.Data;

public sealed class ProcessingJobConfiguration : IEntityTypeConfiguration<ProcessingJob>
{
    public void Configure(EntityTypeBuilder<ProcessingJob> entity)
    {
        entity.ToTable("processing_jobs");
        entity.HasKey(job => job.Id);
        entity.Property(job => job.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        entity.Property(job => job.ErrorMessage).HasMaxLength(2048);
        entity.HasIndex(job => job.FileItemId).IsUnique();
        entity.HasIndex(job => job.Status);
        entity.HasOne(job => job.FileItem)
            .WithOne(file => file.ProcessingJob)
            .HasForeignKey<ProcessingJob>(job => job.FileItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
