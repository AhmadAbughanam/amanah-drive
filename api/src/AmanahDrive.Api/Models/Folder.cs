namespace AmanahDrive.Api.Models;

public sealed class Folder
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public AdminUser User { get; set; } = null!;

    public required string Name { get; set; }

    public Guid? ParentFolderId { get; set; }

    public Folder? ParentFolder { get; set; }

    public ICollection<Folder> ChildFolders { get; set; } = [];

    public ICollection<FileItem> Files { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
