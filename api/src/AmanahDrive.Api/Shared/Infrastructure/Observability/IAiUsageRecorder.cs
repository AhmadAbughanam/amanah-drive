namespace AmanahDrive.Api.Shared.Infrastructure.Observability;

public interface IAiUsageRecorder
{
    Task RecordAsync(AiUsageMeasurement measurement, CancellationToken cancellationToken);
}

public sealed record AiUsageMeasurement(
    string Provider,
    string? Model,
    string Operation,
    int? InputTokens,
    int? OutputTokens,
    long LatencyMilliseconds,
    bool Succeeded,
    string? ErrorType,
    DateTimeOffset OccurredAt);
