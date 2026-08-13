namespace AmanahDrive.Api.Shared.Infrastructure.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
    private const string DocsContentSecurityPolicy = "default-src 'none'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data: https://fonts.scalar.com; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
        context.Response.Headers.TryAdd("Content-Security-Policy", IsOpenApiDocsRequest(context)
            ? DocsContentSecurityPolicy
            : ApiContentSecurityPolicy);

        await next(context);
    }

    private static bool IsOpenApiDocsRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/docs", StringComparison.OrdinalIgnoreCase);
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
