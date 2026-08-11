using AmanahDrive.Api.Modules.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmanahDrive.Api.Modules.Auth.Data;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
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
    }
}
