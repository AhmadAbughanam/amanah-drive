namespace AmanahDrive.Api.Modules.Admin.Observability;

public interface ITraceReader
{
    Task<TraceSearchResponse> SearchAsync(
        string? range,
        string? service,
        int limit,
        CancellationToken cancellationToken);
}

public sealed record TraceSearchResponse(
    bool Available,
    string? Message,
    string Range,
    DateTimeOffset From,
    DateTimeOffset To,
    string? Service,
    IReadOnlyList<string> Services,
    IReadOnlyList<TraceSummary> Traces);

public sealed record TraceSummary(
    string TraceId,
    string RootService,
    string OperationName,
    DateTimeOffset StartTime,
    double DurationMilliseconds,
    int SpanCount,
    int ServiceCount,
    bool HasError,
    IReadOnlyList<string> Services,
    IReadOnlyList<TraceSpanSummary> Spans);

public sealed record TraceSpanSummary(
    string SpanId,
    string? ParentSpanId,
    string Service,
    string OperationName,
    DateTimeOffset StartTime,
    double DurationMilliseconds,
    double OffsetMilliseconds,
    int Depth,
    bool HasError);
