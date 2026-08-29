using System.ComponentModel.DataAnnotations;

namespace AmanahDrive.Api.Shared.Infrastructure.Logging;

public sealed class FileLoggingOptions
{
    public const string SectionName = "LoggingFiles";

    [Required]
    public string DirectoryPath { get; set; } = "logs";

    [Required]
    public string FileNamePrefix { get; set; } = "api-";

    [Range(1, 365)]
    public int RetainedFileCountLimit { get; set; } = 30;

    [Range(1_048_576, long.MaxValue)]
    public long FileSizeLimitBytes { get; set; } = 25 * 1024 * 1024;

    [Range(1, 500)]
    public int DefaultPageSize { get; set; } = 50;

    [Range(1, 500)]
    public int MaxPageSize { get; set; } = 200;

    public string GetRollingFilePath() => Path.Combine(DirectoryPath, $"{FileNamePrefix}.clef");
}
