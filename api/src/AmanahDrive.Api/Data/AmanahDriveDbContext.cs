using AmanahDrive.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AmanahDrive.Api.Data;

public sealed class AmanahDriveDbContext(DbContextOptions<AmanahDriveDbContext> options) : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Folder> Folders => Set<Folder>();

    public DbSet<FileItem> FileItems => Set<FileItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("admin_users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.NormalizedEmail).HasMaxLength(320).IsRequired();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(token => token.CreatedByIp).HasMaxLength(64);
            entity.Property(token => token.RevokedByIp).HasMaxLength(64);
            entity.Property(token => token.ReplacedByTokenHash).HasMaxLength(128);
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasIndex(token => token.UserId);
            entity.HasOne(token => token.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Folder>(entity =>
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
        });

        modelBuilder.Entity<FileItem>(entity =>
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
        });
    }
}
