using System.Threading.RateLimiting;
using AmanahDrive.Api.Ai;
using AmanahDrive.Api.Auth;
using AmanahDrive.Api.Data;
using AmanahDrive.Api.Endpoints;
using AmanahDrive.Api.Options;
using AmanahDrive.Api.Processing;
using AmanahDrive.Api.Search;
using AmanahDrive.Api.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
const string CorsPolicyName = "dashboard";

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.JwtSigningKey.Length >= 32, "Auth:JwtSigningKey must be at least 32 characters.")
    .ValidateOnStart();

builder.Services.AddOptions<DriveOptions>()
    .Bind(builder.Configuration.GetSection(DriveOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.MaxFileSizeBytes > 0, "Drive:MaxFileSizeBytes must be greater than zero.")
    .Validate(options => options.AllowedContentTypes.Length > 0, "Drive:AllowedContentTypes must contain at least one content type.")
    .Validate(options => options.DefaultPageSize <= options.MaxPageSize, "Drive:DefaultPageSize must be less than or equal to Drive:MaxPageSize.")
    .ValidateOnStart();

var driveOptions = builder.Configuration.GetSection(DriveOptions.SectionName).Get<DriveOptions>() ?? new DriveOptions();

builder.Services.AddOptions<AiServiceOptions>()
    .Bind(builder.Configuration.GetSection(AiServiceOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.ServiceToken.Length >= 16, "AiService:ServiceToken must be at least 16 characters.")
    .Validate(options => options.ChunkSize > 0, "AiService:ChunkSize must be greater than zero.")
    .Validate(options => options.ChunkOverlap >= 0 && options.ChunkOverlap < options.ChunkSize, "AiService:ChunkOverlap must be smaller than ChunkSize.")
    .Validate(options => options.WorkerPollSeconds > 0, "AiService:WorkerPollSeconds must be greater than zero.")
    .ValidateOnStart();

var aiServiceOptions = builder.Configuration.GetSection(AiServiceOptions.SectionName).Get<AiServiceOptions>() ?? new AiServiceOptions();

builder.Services.AddOptions<SearchOptions>()
    .Bind(builder.Configuration.GetSection(SearchOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.ChatDefaultPageSize <= options.ChatMaxPageSize, "Search:ChatDefaultPageSize must be less than or equal to Search:ChatMaxPageSize.")
    .ValidateOnStart();

var searchOptions = builder.Configuration.GetSection(SearchOptions.SectionName).Get<SearchOptions>() ?? new SearchOptions();

builder.Services.AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.AllowedOrigins.Length > 0, "Cors:AllowedOrigins must contain at least one origin.")
    .Validate(options => !options.AllowedOrigins.Contains("*", StringComparer.Ordinal), "Cors:AllowedOrigins must not contain '*'.")
    .ValidateOnStart();

var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = driveOptions.MaxFileSizeBytes + 1024 * 1024;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = driveOptions.MaxFileSizeBytes + 1024 * 1024;
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["POSTGRES_CONNECTION_STRING"]
    ?? throw new InvalidOperationException("Database connection string is not configured.");

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

builder.Services.AddDbContext<AmanahDriveDbContext>(options =>
{
    options.UseNpgsql(dataSource, npgsqlOptions => npgsqlOptions.UseVector());
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
    }
});

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
    ?? throw new InvalidOperationException("Auth configuration is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});
builder.Services.AddCors(options =>
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher, Argon2idPasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<ISemanticSearchService, SemanticSearchService>();
builder.Services.AddScoped<ProcessingJobRunner>();
builder.Services.AddHttpClient<IAiProcessingClient, AiProcessingClient>(client =>
{
    client.BaseAddress = new Uri(aiServiceOptions.BaseUrl);
});

if (aiServiceOptions.WorkerEnabled)
{
    builder.Services.AddHostedService<DocumentProcessingWorker>();
}

builder.Services.AddRateLimiter(options =>
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

    options.AddPolicy("ai", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = searchOptions.RateLimitPermitLimit,
                Window = TimeSpan.FromMinutes(searchOptions.RateLimitWindowMinutes),
                QueueLimit = 0
            }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'");
    await next();
});

app.UseCors(CorsPolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapDriveEndpoints();
app.MapSearchChatEndpoints();

app.Run();

public partial class Program;
