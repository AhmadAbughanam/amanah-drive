using Microsoft.AspNetCore.RateLimiting;

namespace AmanahDrive.Api.Shared.Infrastructure.Security;

public static class SecurityRateLimitLogging
{
    public static ValueTask LogRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("AmanahDrive.Api.Security.RateLimiting");
        logger.LogWarning(
            "Rate limit rejected {RequestMethod} {RequestPath} from {RemoteIpAddress} {Category} {SecurityEvent}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path.Value,
            context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            "Security",
            "RateLimitHit");
        return ValueTask.CompletedTask;
    }
}
