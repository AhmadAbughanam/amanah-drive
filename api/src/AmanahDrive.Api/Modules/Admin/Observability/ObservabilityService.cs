using System.Globalization;
using AmanahDrive.Api.Modules.Admin.Logging;
using AmanahDrive.Api.Modules.Admin.Models;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AmanahDrive.Api.Modules.Admin.Observability;

public sealed class ObservabilityService(
    ILogReader logReader,
    AmanahDriveDbContext dbContext) : IObservabilityService
{
    private static readonly string[] OrderedLevels = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    public async Task<ObservabilitySnapshot> GetSnapshotAsync(string? range, CancellationToken cancellationToken)
    {
        var window = ObservabilityWindow.Create(range, DateTimeOffset.UtcNow);
        var logs = await logReader.ReadRangeAsync(window.From, window.To, cancellationToken);
        var requestLogs = logs.Where(IsRequestLog).ToArray();
        var securityLogs = logs
            .Where(entry => CompactJsonLogReader.HasPropertyValue(entry, "Category", "Security"))
            .ToArray();
        var aiUsage = await dbContext.AiUsageRecords
            .AsNoTracking()
            .Where(record => record.OccurredAt >= window.From && record.OccurredAt <= window.To)
            .OrderBy(record => record.OccurredAt)
            .ToListAsync(cancellationToken);

        var monthStart = new DateTimeOffset(window.To.Year, window.To.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthUsage = await dbContext.AiUsageRecords
            .AsNoTracking()
            .Where(record => record.OccurredAt >= monthStart && record.OccurredAt <= window.To)
            .ToListAsync(cancellationToken);

        var requestErrors = requestLogs.Count(entry => ReadInt(entry, "StatusCode") >= 500);
        var requestsToday = requestLogs.Count(entry => entry.Timestamp >= window.To.Date);
        var averageLatency = requestLogs.Length == 0
            ? 0
            : requestLogs.Average(entry => ReadDouble(entry, "Elapsed"));
        var billableMonthUsage = monthUsage.Where(record => !record.Provider.Equals("local", StringComparison.OrdinalIgnoreCase)).ToArray();

        return new ObservabilitySnapshot(
            window.Name,
            window.From,
            window.To,
            new ObservabilityStats(
                requestsToday,
                Percentage(requestErrors, requestLogs.Length),
                Math.Round(averageLatency, 2),
                monthUsage.Sum(record => record.EstimatedCostUsd ?? 0m),
                billableMonthUsage.All(record => record.EstimatedCostUsd is not null)),
            BuildRequestSeries(window, requestLogs),
            OrderedLevels.Select(level => new LogLevelCount(level, logs.Count(entry => entry.Level.Equals(level, StringComparison.OrdinalIgnoreCase)))).ToArray(),
            BuildAiSeries(window, aiUsage),
            BuildSecuritySeries(window, securityLogs),
            securityLogs
                .OrderByDescending(entry => entry.Timestamp)
                .Take(20)
                .Select(entry => new SecurityEventSummary(
                    entry.Timestamp,
                    ReadString(entry, "SecurityEvent") ?? "SecurityEvent",
                    entry.Message,
                    CompactJsonLogReader.GetSourceContext(entry)))
                .ToArray(),
            BuildTopErrors(logs));
    }

    private static IReadOnlyList<RequestMetricPoint> BuildRequestSeries(ObservabilityWindow window, IReadOnlyCollection<LogEntry> entries) =>
        window.Buckets.Select(bucket =>
        {
            var bucketEntries = entries.Where(entry => window.GetBucket(entry.Timestamp) == bucket).ToArray();
            var errors = bucketEntries.Count(entry => ReadInt(entry, "StatusCode") >= 500);
            return new RequestMetricPoint(bucket, bucketEntries.Length, errors, Percentage(errors, bucketEntries.Length));
        }).ToArray();

    private static IReadOnlyList<AiUsageMetricPoint> BuildAiSeries(
        ObservabilityWindow window,
        IReadOnlyCollection<AiUsageRecord> records) =>
        window.Buckets.Select(bucket =>
        {
            var bucketRecords = records.Where(record => window.GetBucket(record.OccurredAt) == bucket).ToArray();
            return new AiUsageMetricPoint(
                bucket,
                bucketRecords.Sum(record => record.InputTokens ?? 0),
                bucketRecords.Sum(record => record.OutputTokens ?? 0),
                bucketRecords.Sum(record => record.EstimatedCostUsd ?? 0m),
                bucketRecords.Length,
                bucketRecords.Count(record => !record.Succeeded),
                bucketRecords.Count(record =>
                    !record.Provider.Equals("local", StringComparison.OrdinalIgnoreCase) &&
                    record.EstimatedCostUsd is null));
        }).ToArray();

    private static IReadOnlyList<SecurityMetricPoint> BuildSecuritySeries(
        ObservabilityWindow window,
        IReadOnlyCollection<LogEntry> entries) =>
        window.Buckets
            .Select(bucket => new SecurityMetricPoint(bucket, entries.Count(entry => window.GetBucket(entry.Timestamp) == bucket)))
            .ToArray();

    private static IReadOnlyList<TopErrorSummary> BuildTopErrors(IReadOnlyCollection<LogEntry> logs) => logs
        .Where(entry => CompactJsonLogReader.GetLevelRank(entry.Level) >= CompactJsonLogReader.GetLevelRank("Warning"))
        .GroupBy(entry => new
        {
            Message = entry.Message,
            ExceptionType = GetExceptionType(entry.Exception),
            entry.Level
        })
        .Select(group => new TopErrorSummary(
            group.Key.ExceptionType is null ? group.Key.Message : $"{group.Key.ExceptionType}: {group.Key.Message}",
            group.Key.Message,
            group.Key.ExceptionType,
            group.Key.Level,
            group.Count(),
            group.Max(entry => entry.Timestamp)))
        .OrderByDescending(error => error.Count)
        .ThenByDescending(error => error.LastSeen)
        .Take(10)
        .ToArray();

    private static bool IsRequestLog(LogEntry entry) =>
        entry.Properties.ContainsKey("RequestPath") &&
        entry.Properties.ContainsKey("StatusCode") &&
        entry.Properties.ContainsKey("Elapsed");

    private static int ReadInt(LogEntry entry, string key)
    {
        if (!entry.Properties.TryGetValue(key, out var value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            int integer => integer,
            long integer => (int)integer,
            double number => (int)number,
            decimal number => (int)number,
            _ when int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };
    }

    private static double ReadDouble(LogEntry entry, string key)
    {
        if (!entry.Properties.TryGetValue(key, out var value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            double number => number,
            float number => number,
            decimal number => (double)number,
            int integer => integer,
            long integer => integer,
            _ when double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };
    }

    private static string? ReadString(LogEntry entry, string key) =>
        entry.Properties.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static string? GetExceptionType(string? exception)
    {
        if (string.IsNullOrWhiteSpace(exception))
        {
            return null;
        }

        var firstLine = exception.Split('\n', 2)[0].Trim();
        var separator = firstLine.IndexOf(':', StringComparison.Ordinal);
        return separator > 0 ? firstLine[..separator] : firstLine;
    }

    private static double Percentage(int numerator, int denominator) =>
        denominator == 0 ? 0 : Math.Round(numerator * 100d / denominator, 2);

    private sealed record ObservabilityWindow(
        string Name,
        DateTimeOffset From,
        DateTimeOffset To,
        TimeSpan BucketSize,
        IReadOnlyList<DateTimeOffset> Buckets)
    {
        public static ObservabilityWindow Create(string? requestedRange, DateTimeOffset now)
        {
            var name = requestedRange?.Trim().ToLowerInvariant() switch
            {
                "7d" => "7d",
                "30d" => "30d",
                _ => "24h"
            };
            var bucketSize = name == "24h" ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
            var bucketCount = name switch
            {
                "7d" => 7,
                "30d" => 30,
                _ => 24
            };
            var currentBucket = name == "24h"
                ? new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero)
                : now.Date;
            var buckets = Enumerable.Range(0, bucketCount)
                .Select(index => currentBucket - TimeSpan.FromTicks(bucketSize.Ticks * (bucketCount - index - 1L)))
                .ToArray();

            return new ObservabilityWindow(name, buckets[0], now, bucketSize, buckets);
        }

        public DateTimeOffset GetBucket(DateTimeOffset timestamp)
        {
            var utc = timestamp.ToUniversalTime();
            return BucketSize == TimeSpan.FromHours(1)
                ? new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero)
                : utc.Date;
        }
    }
}
