namespace AmanahDrive.Api.Modules.Admin.Observability;

public interface IObservabilityService
{
    Task<ObservabilitySnapshot> GetSnapshotAsync(string? range, CancellationToken cancellationToken);
}

public sealed record ObservabilitySnapshot(
    string Range,
    DateTimeOffset From,
    DateTimeOffset To,
    ObservabilityStats Stats,
    IReadOnlyList<RequestMetricPoint> Requests,
    IReadOnlyList<LogLevelCount> LogLevels,
    IReadOnlyList<AiUsageMetricPoint> AiUsage,
    IReadOnlyList<SecurityMetricPoint> Security,
    IReadOnlyList<SecurityEventSummary> RecentSecurityEvents,
    IReadOnlyList<TopErrorSummary> TopErrors);

public sealed record ObservabilityStats(
    int RequestsToday,
    double ErrorRatePercent,
    double AverageLatencyMilliseconds,
    decimal AiSpendThisMonthUsd,
    bool AiPricingComplete);

public sealed record RequestMetricPoint(DateTimeOffset Timestamp, int Requests, int Errors, double ErrorRatePercent);

public sealed record LogLevelCount(string Level, int Count);

public sealed record AiUsageMetricPoint(
    DateTimeOffset Timestamp,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    int Operations,
    int Failures,
    int UnpricedOperations);

public sealed record SecurityMetricPoint(DateTimeOffset Timestamp, int Events);

public sealed record SecurityEventSummary(
    DateTimeOffset Timestamp,
    string Event,
    string Message,
    string Source);

public sealed record TopErrorSummary(
    string Signature,
    string Message,
    string? ExceptionType,
    string Level,
    int Count,
    DateTimeOffset LastSeen);
