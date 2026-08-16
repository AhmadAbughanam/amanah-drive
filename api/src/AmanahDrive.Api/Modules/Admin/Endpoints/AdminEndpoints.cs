using AmanahDrive.Api.Modules.Admin.Logging;
using AmanahDrive.Api.Shared.Infrastructure.Logging;
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

        return app;
    }

    private static Task<LogPage> GetLogsAsync(
        string? level,
        string? search,
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
            new LogQuery(level, search, normalizedPage, normalizedPageSize),
            cancellationToken);
    }
}
