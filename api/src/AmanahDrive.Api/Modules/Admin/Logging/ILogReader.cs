namespace AmanahDrive.Api.Modules.Admin.Logging;

public interface ILogReader
{
    Task<LogPage> ReadAsync(LogQuery query, CancellationToken cancellationToken);
}

public sealed record LogQuery(string? Level, string? Search, int Page, int PageSize);

public sealed record LogPage(int Page, int PageSize, bool HasMore, IReadOnlyList<LogEntry> Entries);

public sealed record LogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Message,
    string? Exception,
    IReadOnlyDictionary<string, object?> Properties);
