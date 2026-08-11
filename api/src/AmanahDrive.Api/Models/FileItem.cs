namespace AmanahDrive.Api.Models;

public sealed class FileItem
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public AdminUser User { get; set; } = null!;

    public Guid? FolderId { get; set; }

    public Folder? Folder { get; set; }

    public required string OriginalFileName { get; set; }

    public required string StorageKey { get; set; }

    public required string ContentType { get; set; }

    public long SizeBytes { get; set; }

    public required string ChecksumSha256 { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
