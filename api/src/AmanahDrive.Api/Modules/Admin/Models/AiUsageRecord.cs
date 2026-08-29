namespace AmanahDrive.Api.Modules.Admin.Models;

public sealed class AiUsageRecord
{
    public Guid Id { get; set; }

    public required string Provider { get; set; }

    public string? Model { get; set; }

    public required string Operation { get; set; }

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    public long LatencyMilliseconds { get; set; }

    public bool Succeeded { get; set; }

    public decimal? EstimatedCostUsd { get; set; }

    public string? ErrorType { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
