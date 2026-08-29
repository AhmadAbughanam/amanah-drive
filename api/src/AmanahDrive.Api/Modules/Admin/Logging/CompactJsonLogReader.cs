using System.Text.Json;
using System.Text.RegularExpressions;
using AmanahDrive.Api.Shared.Infrastructure.Logging;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Modules.Admin.Logging;

public sealed class CompactJsonLogReader(IOptions<FileLoggingOptions> options) : ILogReader
{
    private static readonly Regex BearerTokenPattern = new(
        @"(?i)(Bearer\s+)[A-Za-z0-9._~+\-/]+=*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CredentialValuePattern = new(
        @"(?i)\b(password|access[_-]?token|refresh[_-]?token|service[_-]?token|api[_-]?key|authorization|cookie|secret)\b\s*[:=]\s*([^\s,;]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly FileLoggingOptions _options = options.Value;

    public async Task<LogPage> ReadAsync(LogQuery query, CancellationToken cancellationToken)
    {
        var skip = (long)(query.Page - 1) * query.PageSize;
        var entries = new List<LogEntry>(query.PageSize + 1);
        long matched = 0;

        if (!Directory.Exists(_options.DirectoryPath))
        {
            return new LogPage(query.Page, query.PageSize, false, []);
        }

        var files = Directory.EnumerateFiles(_options.DirectoryPath, $"{_options.FileNamePrefix}*.clef")
            .OrderByDescending(File.GetLastWriteTimeUtc);

        foreach (var file in files)
        {
            List<string> lines;
            try
            {
                lines = await ReadLinesAsync(file, cancellationToken);
            }
            catch (IOException)
            {
                // A retention roll can remove a file between enumeration and opening it.
                continue;
            }

            for (var index = lines.Count - 1; index >= 0; index--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryParse(lines[index], out var entry) || !Matches(entry, query))
                {
                    continue;
                }

                if (matched++ < skip)
                {
                    continue;
                }

                entries.Add(entry);
                if (entries.Count > query.PageSize)
                {
                    return new LogPage(query.Page, query.PageSize, true, entries.Take(query.PageSize).ToArray());
                }
            }
        }

        return new LogPage(query.Page, query.PageSize, false, entries);
    }

    public async Task<IReadOnlyList<LogEntry>> ReadRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_options.DirectoryPath))
        {
            return [];
        }

        var entries = new List<LogEntry>();
        var files = Directory.EnumerateFiles(_options.DirectoryPath, $"{_options.FileNamePrefix}*.clef")
            .OrderBy(File.GetLastWriteTimeUtc);

        foreach (var file in files)
        {
            List<string> lines;
            try
            {
                lines = await ReadLinesAsync(file, cancellationToken);
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryParse(line, out var entry) && entry.Timestamp >= from && entry.Timestamp <= to)
                {
                    entries.Add(entry);
                }
            }
        }

        return entries.OrderBy(entry => entry.Timestamp).ToArray();
    }

    private static async Task<List<string>> ReadLinesAsync(string file, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static bool Matches(LogEntry entry, LogQuery query)
    {
        if (query.From is not null && entry.Timestamp < query.From.Value)
        {
            return false;
        }

        if (query.To is not null && entry.Timestamp > query.To.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Level) &&
            !entry.Level.Equals(NormalizeLevel(query.Level), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!MatchesCategory(entry, query.Category) || !MatchesSource(entry, query.Source))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.Search))
        {
            return true;
        }

        var search = query.Search.Trim();
        return entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               (entry.Exception?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
               entry.Properties.Any(property =>
                   property.Key.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   (property.Value?.ToString()?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static bool MatchesCategory(LogEntry entry, string? category) => category?.Trim().ToLowerInvariant() switch
    {
        null or "" => true,
        "security" => HasPropertyValue(entry, "Category", "Security"),
        "ai" or "ai/cost" => HasPropertyValue(entry, "Category", "AI"),
        "errors" => GetLevelRank(entry.Level) >= GetLevelRank("Warning"),
        "api" => !HasPropertyValue(entry, "Category", "Security") &&
                 !HasPropertyValue(entry, "Category", "AI") &&
                 (entry.Properties.ContainsKey("RequestPath") || GetSourceContext(entry).Contains("AmanahDrive.Api", StringComparison.OrdinalIgnoreCase)),
        _ => false
    };

    private static bool MatchesSource(LogEntry entry, string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return true;
        }

        var normalized = source.Trim();
        var sourceContext = GetSourceContext(entry);
        return sourceContext.Contains($".Modules.{normalized}.", StringComparison.OrdinalIgnoreCase) ||
               sourceContext.EndsWith($".Modules.{normalized}", StringComparison.OrdinalIgnoreCase) ||
               sourceContext.Contains($".{normalized}.", StringComparison.OrdinalIgnoreCase);
    }

    internal static int GetLevelRank(string level) => NormalizeLevel(level) switch
    {
        "Verbose" => 0,
        "Debug" => 1,
        "Information" => 2,
        "Warning" => 3,
        "Error" => 4,
        "Fatal" => 5,
        _ => -1
    };

    internal static string GetSourceContext(LogEntry entry) =>
        entry.Properties.TryGetValue("SourceContext", out var value) ? value?.ToString() ?? string.Empty : string.Empty;

    internal static bool HasPropertyValue(LogEntry entry, string key, string expected) =>
        entry.Properties.TryGetValue(key, out var value) &&
        string.Equals(value?.ToString(), expected, StringComparison.OrdinalIgnoreCase);

    private static bool TryParse(string line, out LogEntry entry)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("@t", out var timestampProperty) || !timestampProperty.TryGetDateTimeOffset(out var timestamp))
            {
                entry = null!;
                return false;
            }

            var message = RedactText(ReadString(root, "@m") ?? ReadString(root, "@mt") ?? string.Empty) ?? string.Empty;
            var properties = root.EnumerateObject()
                .Where(property => !property.Name.StartsWith('@'))
                .ToDictionary(
                    property => property.Name,
                    property => IsSensitiveProperty(property.Name)
                        ? (object?)"[REDACTED]"
                        : ToSerializableValue(property.Value));

            entry = new LogEntry(
                timestamp,
                ReadLevel(root),
                message,
                RedactText(ReadString(root, "@x")),
                properties);
            return true;
        }
        catch (JsonException)
        {
            entry = null!;
            return false;
        }
    }

    private static string ReadLevel(JsonElement root)
    {
        var value = ReadString(root, "@l");
        return string.IsNullOrWhiteSpace(value) ? "Information" : NormalizeLevel(value);
    }

    internal static string NormalizeLevel(string level) => level.Trim().ToUpperInvariant() switch
    {
        "VRB" or "VERBOSE" or "TRACE" => "Verbose",
        "DBG" or "DEBUG" => "Debug",
        "INF" or "INFORMATION" or "INFO" => "Information",
        "WRN" or "WARNING" or "WARN" => "Warning",
        "ERR" or "ERROR" => "Error",
        "FTL" or "FATAL" or "CRITICAL" => "Fatal",
        _ => level.Trim()
    };

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool IsSensitiveProperty(string name)
    {
        var normalized = name.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return normalized.Contains("password", StringComparison.Ordinal) ||
               normalized.Contains("authorization", StringComparison.Ordinal) ||
               normalized.Contains("cookie", StringComparison.Ordinal) ||
               normalized.Contains("secret", StringComparison.Ordinal) ||
               normalized.Contains("apikey", StringComparison.Ordinal) ||
               normalized.Contains("accesstoken", StringComparison.Ordinal) ||
               normalized.Contains("refreshtoken", StringComparison.Ordinal) ||
               normalized.Contains("servicetoken", StringComparison.Ordinal) ||
               normalized.Contains("tokenhash", StringComparison.Ordinal);
    }

    private static string? RedactText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var withoutBearer = BearerTokenPattern.Replace(value, "$1[REDACTED]");
        return CredentialValuePattern.Replace(withoutBearer, "$1=[REDACTED]");
    }

    private static object? ToSerializableValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when value.TryGetDouble(out var number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => value.EnumerateArray().Select(ToSerializableValue).ToArray(),
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(
            property => property.Name,
            property => IsSensitiveProperty(property.Name)
                ? (object?)"[REDACTED]"
                : ToSerializableValue(property.Value)),
        _ => value.ToString()
    };
}
