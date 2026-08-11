using System.ComponentModel.DataAnnotations;
using AmanahDrive.Api.Auth;
using AmanahDrive.Api.Options;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validationResult = ValidateRequest(request);
            if (validationResult is not null)
            {
                return validationResult;
            }

            var result = await authService.RegisterAsync(
                request.Email,
                request.Password,
                httpContext.Request.Headers["X-Bootstrap-Token"].FirstOrDefault(),
                httpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return ToHttpResult(result, httpContext);
        }).RequireRateLimiting("login");

        group.MapPost("/login", async (
            LoginRequest request,
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validationResult = ValidateRequest(request);
            if (validationResult is not null)
            {
                return validationResult;
            }

            var result = await authService.LoginAsync(
                request.Email,
                request.Password,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return ToHttpResult(result, httpContext);
        }).RequireRateLimiting("login");

        group.MapPost("/refresh", async (
            HttpContext httpContext,
            IAuthService authService,
            IOptions<AuthOptions> options,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.RefreshAsync(
                httpContext.Request.Cookies[options.Value.RefreshCookieName],
                httpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return ToHttpResult(result, httpContext);
        });

        group.MapPost("/logout", async (
            HttpContext httpContext,
            IAuthService authService,
            IOptions<AuthOptions> options,
            CancellationToken cancellationToken) =>
        {
            await authService.LogoutAsync(
                httpContext.Request.Cookies[options.Value.RefreshCookieName],
                httpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            DeleteRefreshCookie(httpContext, options.Value);
            return Results.NoContent();
        });

        return group;
    }

    private static IResult? ValidateRequest<TRequest>(TRequest request)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(request!);
        if (Validator.TryValidateObject(request!, context, validationResults, validateAllProperties: true))
        {
            return null;
        }

        return Results.ValidationProblem(validationResults.ToDictionary(
            result => result.MemberNames.FirstOrDefault() ?? string.Empty,
            result => new[] { result.ErrorMessage ?? "Invalid value." }));
    }

    private static IResult ToHttpResult(AuthResult result, HttpContext httpContext)
    {
        if (result.Status == AuthResultStatus.Success)
        {
            var options = httpContext.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value;
            SetRefreshCookie(httpContext, options, result.RefreshToken!);
            return Results.Ok(new AuthResponse(result.AccessToken!));
        }

        return result.Status switch
        {
            AuthResultStatus.Unauthorized => Results.Unauthorized(),
            AuthResultStatus.Forbidden => Results.Problem(result.Error, statusCode: StatusCodes.Status403Forbidden),
            AuthResultStatus.Conflict => Results.Conflict(new ErrorResponse(result.Error ?? "Conflict.")),
            AuthResultStatus.Locked => Results.Problem(result.Error, statusCode: StatusCodes.Status423Locked),
            _ => Results.Problem("Unexpected authentication result.")
        };
    }

    private static void SetRefreshCookie(HttpContext httpContext, AuthOptions options, string refreshToken)
    {
        httpContext.Response.Cookies.Append(options.RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.SecureCookies,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(options.RefreshTokenDays)
        });
    }

    private static void DeleteRefreshCookie(HttpContext httpContext, AuthOptions options)
    {
        httpContext.Response.Cookies.Delete(options.RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.SecureCookies,
            SameSite = SameSiteMode.Strict
        });
    }
}

public sealed record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(12)] string Password);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record AuthResponse(string AccessToken);

public sealed record ErrorResponse(string Message);
