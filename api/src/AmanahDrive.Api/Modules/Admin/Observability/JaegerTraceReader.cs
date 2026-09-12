using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using OpenTelemetry;

namespace AmanahDrive.Api.Modules.Admin.Observability;

public sealed class JaegerTraceReader(
    HttpClient httpClient,
    ILogger<JaegerTraceReader> logger) : ITraceReader
{
    private const string PreferredService = "amanah-drive-api";

    public async Task<TraceSearchResponse> SearchAsync(
        string? range,
        string? service,
        int limit,
        CancellationToken cancellationToken)
    {
        var window = TraceWindow.Create(range, DateTimeOffset.UtcNow);

        try
        {
            using var instrumentationScope = SuppressInstrumentationScope.Begin();
            var servicesResponse = await httpClient.GetFromJsonAsync<JaegerServicesResponse>(
                "api/services",
                cancellationToken);
            var services = servicesResponse?.Data?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray() ?? [];

            if (services.Length == 0)
            {
                return new TraceSearchResponse(
                    true,
                    "Jaeger is connected, but no trace-producing services have reported data in its current in-memory session.",
                    window.Name,
                    window.From,
                    window.To,
                    null,
                    services,
                    []);
            }

            var selectedService = SelectService(service, services);
            var requestUri = string.Create(
                CultureInfo.InvariantCulture,
                $"api/traces?service={Uri.EscapeDataString(selectedService)}&start={ToUnixMicroseconds(window.From)}&end={ToUnixMicroseconds(window.To)}&limit={limit}");
            var tracesResponse = await httpClient.GetFromJsonAsync<JaegerTracesResponse>(
                requestUri,
                cancellationToken);
            var traces = tracesResponse?.Data?
                .Select(MapTrace)
                .Where(trace => trace is not null)
                .Cast<TraceSummary>()
                .OrderByDescending(trace => trace.StartTime)
                .ToArray() ?? [];

            return new TraceSearchResponse(
                true,
                null,
                window.Name,
                window.From,
                window.To,
                selectedService,
                services,
                traces);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
            logger.LogWarning(exception, "Unable to query Jaeger trace data");
            return new TraceSearchResponse(
                false,
                "Trace storage is currently unavailable. Metrics and logs are still available.",
                window.Name,
                window.From,
                window.To,
                service,
                [],
                []);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Jaeger trace query timed out");
            return new TraceSearchResponse(
                false,
                "Trace storage timed out. Metrics and logs are still available.",
                window.Name,
                window.From,
                window.To,
                service,
                [],
                []);
        }
    }

    private static string SelectService(string? requested, IReadOnlyCollection<string> services)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var match = services.FirstOrDefault(value => value.Equals(requested.Trim(), StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }
        }

        return services.FirstOrDefault(value => value.Equals(PreferredService, StringComparison.Ordinal))
            ?? services.First();
    }

    private static TraceSummary? MapTrace(JaegerTrace trace)
    {
        if (trace.Spans is null || trace.Spans.Count == 0)
        {
            return null;
        }

        var processServices = trace.Processes?.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ServiceName ?? "unknown-service",
            StringComparer.Ordinal) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var spanIds = trace.Spans
            .Where(span => !string.IsNullOrWhiteSpace(span.SpanId))
            .Select(span => span.SpanId!)
            .ToHashSet(StringComparer.Ordinal);
        var mapped = trace.Spans
            .Where(span => !string.IsNullOrWhiteSpace(span.SpanId))
            .Select(span => new MappedSpan(
                span,
                ReadParentSpanId(span),
                ReadService(span, processServices),
                HasError(span)))
            .OrderBy(span => span.Source.StartTime)
            .ToArray();

        if (mapped.Length == 0)
        {
            return null;
        }

        var traceStartMicros = mapped.Min(span => span.Source.StartTime);
        var traceEndMicros = mapped.Max(span => span.Source.StartTime + Math.Max(0, span.Source.Duration));
        var root = mapped.FirstOrDefault(span => span.ParentSpanId is null || !spanIds.Contains(span.ParentSpanId))
            ?? mapped[0];
        var depths = mapped.ToDictionary(
            span => span.Source.SpanId!,
            span => CalculateDepth(span, mapped),
            StringComparer.Ordinal);
        var spans = mapped.Select(span => new TraceSpanSummary(
                span.Source.SpanId!,
                span.ParentSpanId,
                span.Service,
                span.Source.OperationName ?? "unnamed operation",
                FromUnixMicroseconds(span.Source.StartTime),
                RoundMilliseconds(span.Source.Duration),
                RoundMilliseconds(span.Source.StartTime - traceStartMicros),
                depths[span.Source.SpanId!],
                span.HasError))
            .ToArray();
        var services = mapped
            .Select(span => span.Service)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new TraceSummary(
            trace.TraceId ?? root.Source.TraceId ?? string.Empty,
            root.Service,
            root.Source.OperationName ?? "unnamed operation",
            FromUnixMicroseconds(traceStartMicros),
            RoundMilliseconds(traceEndMicros - traceStartMicros),
            spans.Length,
            services.Length,
            mapped.Any(span => span.HasError),
            services,
            spans);
    }

    private static int CalculateDepth(MappedSpan span, IReadOnlyCollection<MappedSpan> spans)
    {
        var byId = spans.ToDictionary(item => item.Source.SpanId!, StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { span.Source.SpanId! };
        var parentId = span.ParentSpanId;
        var depth = 0;

        while (parentId is not null && byId.TryGetValue(parentId, out var parent) && visited.Add(parentId))
        {
            depth++;
            parentId = parent.ParentSpanId;
        }

        return depth;
    }

    private static string? ReadParentSpanId(JaegerSpan span) => span.References?
        .FirstOrDefault(reference => reference.RefType?.Equals("CHILD_OF", StringComparison.OrdinalIgnoreCase) == true)
        ?.SpanId;

    private static string ReadService(JaegerSpan span, IReadOnlyDictionary<string, string> processServices) =>
        span.ProcessId is not null && processServices.TryGetValue(span.ProcessId, out var service)
            ? service
            : "unknown-service";

    private static bool HasError(JaegerSpan span) => span.Tags?.Any(tag =>
        (tag.Key?.Equals("error", StringComparison.OrdinalIgnoreCase) == true && IsTruthy(tag.Value)) ||
        ((tag.Key?.Equals("otel.status_code", StringComparison.OrdinalIgnoreCase) == true ||
          tag.Key?.Equals("status.code", StringComparison.OrdinalIgnoreCase) == true) &&
         tag.Value.ToString().Equals("ERROR", StringComparison.OrdinalIgnoreCase))) == true;

    private static bool IsTruthy(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.String => bool.TryParse(value.GetString(), out var result) && result,
        JsonValueKind.Number => value.TryGetInt32(out var result) && result != 0,
        _ => false
    };

    private static long ToUnixMicroseconds(DateTimeOffset value) => value.ToUnixTimeMilliseconds() * 1_000;

    private static DateTimeOffset FromUnixMicroseconds(long value) =>
        DateTimeOffset.FromUnixTimeMilliseconds(value / 1_000).AddTicks(value % 1_000 * 10);

    private static double RoundMilliseconds(long microseconds) => Math.Round(microseconds / 1_000d, 2);

    private sealed record MappedSpan(JaegerSpan Source, string? ParentSpanId, string Service, bool HasError);

    private sealed record JaegerServicesResponse(IReadOnlyList<string>? Data);

    private sealed record JaegerTracesResponse(IReadOnlyList<JaegerTrace>? Data);

    private sealed record JaegerTrace(
        string? TraceId,
        IReadOnlyList<JaegerSpan>? Spans,
        IReadOnlyDictionary<string, JaegerProcess>? Processes);

    private sealed record JaegerSpan(
        string? TraceId,
        string? SpanId,
        string? OperationName,
        IReadOnlyList<JaegerReference>? References,
        long StartTime,
        long Duration,
        IReadOnlyList<JaegerTag>? Tags,
        string? ProcessId);

    private sealed record JaegerReference(string? RefType, string? SpanId);

    private sealed record JaegerTag(string? Key, JsonElement Value);

    private sealed record JaegerProcess(string? ServiceName);

    private sealed record TraceWindow(string Name, DateTimeOffset From, DateTimeOffset To)
    {
        public static TraceWindow Create(string? requestedRange, DateTimeOffset now)
        {
            var name = requestedRange?.Trim().ToLowerInvariant() switch
            {
                "7d" => "7d",
                "30d" => "30d",
                _ => "24h"
            };
            var duration = name switch
            {
                "7d" => TimeSpan.FromDays(7),
                "30d" => TimeSpan.FromDays(30),
                _ => TimeSpan.FromHours(24)
            };
            return new TraceWindow(name, now - duration, now);
        }
    }
}
