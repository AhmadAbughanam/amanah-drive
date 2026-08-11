using AmanahDrive.Api.Modules.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmanahDrive.Api.Modules.Auth.Data;

public sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> entity)
    {
        entity.ToTable("admin_users");
        entity.HasKey(user => user.Id);
        entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
        entity.Property(user => user.NormalizedEmail).HasMaxLength(320).IsRequired();
        entity.Property(user => user.PasswordHash).IsRequired();
        entity.HasIndex(user => user.NormalizedEmail).IsUnique();
    }
}
