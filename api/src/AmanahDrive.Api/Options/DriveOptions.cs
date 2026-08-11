using System.ComponentModel.DataAnnotations;

namespace AmanahDrive.Api.Options;

public sealed class DriveOptions
{
    public const string SectionName = "Drive";

    [Required]
    public string StorageRoot { get; init; } = "storage";

    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;

    public string[] AllowedContentTypes { get; init; } =
    [
        "application/pdf",
        "text/markdown",
        "text/plain"
    ];
}
