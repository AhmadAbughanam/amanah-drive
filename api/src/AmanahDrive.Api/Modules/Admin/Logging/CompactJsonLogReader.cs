using System.Text.Json;
using AmanahDrive.Api.Shared.Infrastructure.Logging;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Modules.Admin.Logging;

public sealed class CompactJsonLogReader(IOptions<FileLoggingOptions> options) : ILogReader
{
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
                var line = lines[index];
                if (!Matches(line, query) || !TryParse(line, out var entry))
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

    private static bool Matches(string line, LogQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search) && !line.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.Level))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var level = ReadLevel(document.RootElement);
            return level.Equals(NormalizeLevel(query.Level), StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

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

            var message = ReadString(root, "@m") ?? ReadString(root, "@mt") ?? string.Empty;
            var properties = root.EnumerateObject()
                .Where(property => !property.Name.StartsWith('@'))
                .ToDictionary(property => property.Name, property => ToSerializableValue(property.Value));

            entry = new LogEntry(
                timestamp,
                ReadLevel(root),
                message,
                ReadString(root, "@x"),
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

    private static string NormalizeLevel(string level) => level.Trim().ToUpperInvariant() switch
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

    private static object? ToSerializableValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when value.TryGetDouble(out var number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => value.Clone()
    };
}
