using System.ComponentModel.DataAnnotations;

namespace AmanahDrive.Api.Shared.Infrastructure.Ai;

public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    [Required]
    public string BaseUrl { get; init; } = "http://ai-service:8000";

    [Required]
    public string ServiceToken { get; init; } = string.Empty;

    public int ChunkSize { get; init; } = 1000;

    public int ChunkOverlap { get; init; } = 200;

    public bool WorkerEnabled { get; init; } = true;

    public int WorkerPollSeconds { get; init; } = 5;

    public int RetryMaxAttempts { get; init; } = 2;

    public int RetryBaseDelayMilliseconds { get; init; } = 250;

    public int AttemptTimeoutSeconds { get; init; } = 45;

    public int TotalTimeoutSeconds { get; init; } = 100;

    public int CircuitBreakerMinimumThroughput { get; init; } = 4;

    public int CircuitBreakerSamplingSeconds { get; init; } = 120;

    public int CircuitBreakerBreakSeconds { get; init; } = 30;
}
