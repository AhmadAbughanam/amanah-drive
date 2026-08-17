using System.Text;
using AmanahDrive.Api.Modules.Auth.Options;
using AmanahDrive.Api.Shared.Infrastructure.Ai;
using AmanahDrive.Api.Shared.Infrastructure.Cors;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using AmanahDrive.Api.Shared.Infrastructure.Health;
using AmanahDrive.Api.Shared.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace AmanahDrive.Api.Shared.Infrastructure;

public static class InfrastructureModule
{
    private const string CorsPolicyName = "dashboard";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<AiServiceOptions>()
            .Bind(configuration.GetSection(AiServiceOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.ServiceToken.Length >= 16, "AiService:ServiceToken must be at least 16 characters.")
            .Validate(options => options.ChunkSize > 0, "AiService:ChunkSize must be greater than zero.")
            .Validate(options => options.ChunkOverlap >= 0 && options.ChunkOverlap < options.ChunkSize, "AiService:ChunkOverlap must be smaller than ChunkSize.")
            .Validate(options => options.WorkerPollSeconds > 0, "AiService:WorkerPollSeconds must be greater than zero.")
            .Validate(options => options.RetryMaxAttempts >= 0, "AiService:RetryMaxAttempts must not be negative.")
            .Validate(options => options.RetryBaseDelayMilliseconds > 0, "AiService:RetryBaseDelayMilliseconds must be greater than zero.")
            .Validate(options => options.AttemptTimeoutSeconds > 0, "AiService:AttemptTimeoutSeconds must be greater than zero.")
            .Validate(options => options.TotalTimeoutSeconds > options.AttemptTimeoutSeconds, "AiService:TotalTimeoutSeconds must exceed AttemptTimeoutSeconds.")
            .Validate(options => options.CircuitBreakerMinimumThroughput >= 2, "AiService:CircuitBreakerMinimumThroughput must be at least two.")
            .Validate(options => options.CircuitBreakerSamplingSeconds >= options.AttemptTimeoutSeconds * 2, "AiService:CircuitBreakerSamplingSeconds must be at least twice AttemptTimeoutSeconds.")
            .Validate(options => options.CircuitBreakerBreakSeconds > 0, "AiService:CircuitBreakerBreakSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.AllowedOrigins.Length > 0, "Cors:AllowedOrigins must contain at least one origin.")
            .Validate(options => !options.AllowedOrigins.Contains("*", StringComparer.Ordinal), "Cors:AllowedOrigins must not contain '*'.")
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString("Default")
            ?? configuration["POSTGRES_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("Database connection string is not configured.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton(dataSource);

        services.AddDbContext<AmanahDriveDbContext>(options =>
        {
            options.UseNpgsql(dataSource, npgsqlOptions => npgsqlOptions.UseVector());
            if (environment.IsEnvironment("Testing"))
            {
                options.ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
            }
        });

        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
            ?? throw new InvalidOperationException("Auth configuration is not configured.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = authOptions.JwtIssuer,
                    ValidAudience = authOptions.JwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.JwtSigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy
                    .WithOrigins(corsOptions.AllowedOrigins)
                    .WithHeaders("Authorization", "Content-Type")
                    .WithMethods("GET", "POST", "PATCH", "DELETE", "OPTIONS")
                    .AllowCredentials();
            });
        });

        services.AddAuthorization();
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        var aiServiceOptions = configuration.GetSection(AiServiceOptions.SectionName).Get<AiServiceOptions>() ?? new AiServiceOptions();
        services.AddHttpClient<IAiProcessingClient, AiProcessingClient>(client =>
        {
            client.BaseAddress = new Uri(aiServiceOptions.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        }).AddAiServiceResilience(aiServiceOptions);

        return services;
    }

    public static async Task ApplyPendingMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public static WebApplication UseInfrastructure(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        {
            app.UseHsts();
        }

        app.UseSecurityHeaders();
        app.UseCors(CorsPolicyName);
        return app;
    }
}
