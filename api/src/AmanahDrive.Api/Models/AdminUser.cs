namespace AmanahDrive.Api.Models;

public sealed class AdminUser
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string NormalizedEmail { get; set; }

    public required string PasswordHash { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTimeOffset? LockoutEndsAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    public ICollection<Folder> Folders { get; set; } = [];

    public ICollection<FileItem> Files { get; set; } = [];
}
