namespace AmanahDrive.Api.Modules.Auth.Models;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public AdminUser User { get; set; } = null!;

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedByIp { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedByIp { get; set; }

    public DateTimeOffset? RotatedAt { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
