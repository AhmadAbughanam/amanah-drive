using System.ComponentModel.DataAnnotations;

namespace AmanahDrive.Api.Modules.Drive.Options;

public sealed class DriveOptions
{
    public const string SectionName = "Drive";

    [Required]
    public string StorageRoot { get; init; } = "storage";

    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;

    public string[] AllowedContentTypes { get; init; } =
    [
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "image/jpeg",
        "image/png",
        "text/csv",
        "text/markdown",
        "text/plain"
    ];

    [Range(1, 100)]
    public int DefaultPageSize { get; init; } = 50;

    [Range(1, 500)]
    public int MaxPageSize { get; init; } = 100;
}
