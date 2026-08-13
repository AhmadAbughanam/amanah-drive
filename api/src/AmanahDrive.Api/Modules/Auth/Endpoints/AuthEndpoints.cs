using System.ComponentModel.DataAnnotations;
using AmanahDrive.Api.Modules.Auth.Options;
using AmanahDrive.Api.Shared.Infrastructure.Http;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace AmanahDrive.Api.Modules.Auth.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.WithTags("Auth");

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
        })
            .RequireRateLimiting("login")
            .WithSummary("Bootstrap the single admin account.")
            .WithDescription("Creates the one admin account when the configured X-Bootstrap-Token header is valid.")
            .WithOpenApi(operation =>
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "X-Bootstrap-Token",
                    In = ParameterLocation.Header,
                    Required = true,
                    Description = "Bootstrap token configured outside source control."
                });
                return operation;
            })
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();

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
        })
            .RequireRateLimiting("login")
            .WithSummary("Sign in as the admin user.")
            .WithDescription("Returns a JWT access token and sets the refresh token as an HTTP-only cookie.")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesValidationProblem();

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
        })
            .WithSummary("Rotate the refresh token and issue a new access token.")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

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
        })
            .WithSummary("Revoke the current refresh token and clear the refresh cookie.")
            .Produces(StatusCodes.Status204NoContent);

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
