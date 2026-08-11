using System.Threading.RateLimiting;
using AmanahDrive.Api.Ai;
using AmanahDrive.Api.Auth;
using AmanahDrive.Api.Data;
using AmanahDrive.Api.Endpoints;
using AmanahDrive.Api.Options;
using AmanahDrive.Api.Processing;
using AmanahDrive.Api.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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
    options.UseNpgsql(dataSource, npgsqlOptions => npgsqlOptions.UseVector()));

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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher, Argon2idPasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
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
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapDriveEndpoints();

app.Run();

public partial class Program;
