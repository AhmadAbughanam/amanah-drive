using System.Threading.RateLimiting;
using AmanahDrive.Api.Modules.Auth.Endpoints;
using AmanahDrive.Api.Modules.Auth.Options;

namespace AmanahDrive.Api.Modules.Auth;

public static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.JwtSigningKey.Length >= 32, "Auth:JwtSigningKey must be at least 32 characters.")
            .ValidateOnStart();

        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
            ?? throw new InvalidOperationException("Auth configuration is not configured.");

        services.AddHttpContextAccessor();
        services.AddScoped<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("login", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authOptions.LoginRateLimitPermitLimit,
                        Window = TimeSpan.FromMinutes(authOptions.LoginRateLimitWindowMinutes),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    public static IEndpointRouteBuilder MapAuthModule(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();
        return app;
    }
}
