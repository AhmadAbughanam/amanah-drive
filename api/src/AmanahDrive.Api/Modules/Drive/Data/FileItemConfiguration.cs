using AmanahDrive.Api.Modules.Drive.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmanahDrive.Api.Modules.Drive.Data;

public sealed class FileItemConfiguration : IEntityTypeConfiguration<FileItem>
{
    public void Configure(EntityTypeBuilder<FileItem> entity)
    {
        entity.ToTable("file_items");
        entity.HasKey(file => file.Id);
        entity.Property(file => file.OriginalFileName).HasMaxLength(255).IsRequired();
        entity.Property(file => file.StorageKey).HasMaxLength(128).IsRequired();
        entity.Property(file => file.ContentType).HasMaxLength(255).IsRequired();
        entity.Property(file => file.ChecksumSha256).HasMaxLength(64).IsRequired();
        entity.HasIndex(file => file.UserId);
        entity.HasIndex(file => file.StorageKey).IsUnique();
        entity.HasIndex(file => new { file.UserId, file.FolderId, file.OriginalFileName })
            .IsUnique()
            .HasFilter("\"FolderId\" IS NOT NULL");
        entity.HasIndex(file => new { file.UserId, file.OriginalFileName })
            .IsUnique()
            .HasFilter("\"FolderId\" IS NULL");
        entity.HasOne(file => file.User)
            .WithMany(user => user.Files)
            .HasForeignKey(file => file.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(file => file.Folder)
            .WithMany(folder => folder.Files)
            .HasForeignKey(file => file.FolderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
