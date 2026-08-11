namespace AmanahDrive.Api.Models;

public sealed class ProcessingJob
{
    public Guid Id { get; set; }

    public Guid FileItemId { get; set; }

    public FileItem FileItem { get; set; } = null!;

    public ProcessingJobStatus Status { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? FailedAt { get; set; }
}

public enum ProcessingJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
