using AmanahDrive.Api.Modules.Admin.Logging;
using AmanahDrive.Api.Modules.Admin.Observability;
using AmanahDrive.Api.Modules.Admin.Options;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using AmanahDrive.Api.Shared.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Modules.Admin.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/logs", GetLogsAsync)
            .WithTags("Admin")
            .WithSummary("Return recent persisted API log entries.")
            .Produces<LogPage>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        app.MapGet("/admin/activity", GetActivityAsync)
            .WithTags("Admin")
            .WithSummary("Return recent domain activity entries.")
            .Produces<ActivityPageResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        app.MapGet("/admin/observability", GetObservabilityAsync)
            .WithTags("Admin")
            .WithSummary("Return retained request, error, AI usage, and security metrics.")
            .Produces<ObservabilitySnapshot>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        return app;
    }

    private static Task<LogPage> GetLogsAsync(
        string? level,
        string? search,
        string? category,
        string? source,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? page,
        int? pageSize,
        ILogReader logReader,
        IOptions<FileLoggingOptions> options,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, page ?? 1);
        var normalizedPageSize = Math.Clamp(
            pageSize ?? options.Value.DefaultPageSize,
            1,
            options.Value.MaxPageSize);

        return logReader.ReadAsync(
            new LogQuery(level, search, category, source, from, to, normalizedPage, normalizedPageSize),
            cancellationToken);
    }

    private static Task<ObservabilitySnapshot> GetObservabilityAsync(
        string? range,
        IObservabilityService observabilityService,
        CancellationToken cancellationToken) =>
        observabilityService.GetSnapshotAsync(range, cancellationToken);

    private static async Task<ActivityPageResponse> GetActivityAsync(
        string? type,
        string? search,
        int? page,
        int? pageSize,
        AmanahDriveDbContext dbContext,
        IOptions<ActivityOptions> options,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, page ?? 1);
        var normalizedPageSize = Math.Clamp(
            pageSize ?? options.Value.DefaultPageSize,
            1,
            options.Value.MaxPageSize);
        var skip = (int)Math.Min((long)(normalizedPage - 1) * normalizedPageSize, int.MaxValue);

        var query = dbContext.ActivityEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim().ToLower();
            query = query.Where(entry => entry.Type.ToLower() == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(entry => entry.Summary.ToLower().Contains(normalizedSearch));
        }

        var entries = await query
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Skip(skip)
            .Take(normalizedPageSize + 1)
            .Select(entry => new ActivityEntryResponse(
                entry.Id,
                entry.Type,
                entry.Summary,
                entry.OccurredAt,
                entry.FileId,
                entry.ConversationId))
            .ToListAsync(cancellationToken);
        var hasMore = entries.Count > normalizedPageSize;

        return new ActivityPageResponse(
            normalizedPage,
            normalizedPageSize,
            hasMore,
            entries.Take(normalizedPageSize).ToArray());
    }
}

public sealed record ActivityPageResponse(
    int Page,
    int PageSize,
    bool HasMore,
    IReadOnlyList<ActivityEntryResponse> Entries);

public sealed record ActivityEntryResponse(
    Guid Id,
    string Type,
    string Summary,
    DateTimeOffset OccurredAt,
    Guid? FileId,
    Guid? ConversationId);
