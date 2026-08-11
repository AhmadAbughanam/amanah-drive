using AmanahDrive.Api.Modules.Drive.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmanahDrive.Api.Modules.Drive.Data;

public sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> entity)
    {
        entity.ToTable("folders");
        entity.HasKey(folder => folder.Id);
        entity.Property(folder => folder.Name).HasMaxLength(255).IsRequired();
        entity.HasIndex(folder => folder.UserId);
        entity.HasIndex(folder => new { folder.UserId, folder.ParentFolderId, folder.Name })
            .IsUnique()
            .HasFilter("\"ParentFolderId\" IS NOT NULL");
        entity.HasIndex(folder => new { folder.UserId, folder.Name })
            .IsUnique()
            .HasFilter("\"ParentFolderId\" IS NULL");
        entity.HasOne(folder => folder.User)
            .WithMany(user => user.Folders)
            .HasForeignKey(folder => folder.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(folder => folder.ParentFolder)
            .WithMany(folder => folder.ChildFolders)
            .HasForeignKey(folder => folder.ParentFolderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
