using System.Diagnostics;
using AmanahDrive.Api.Modules.Admin;
using AmanahDrive.Api.Modules.Auth;
using AmanahDrive.Api.Modules.Drive;
using AmanahDrive.Api.Modules.Processing;
using AmanahDrive.Api.Modules.SearchChat;
using AmanahDrive.Api.Shared.Infrastructure;
using AmanahDrive.Api.Shared.Infrastructure.Logging;
using AmanahDrive.Api.Shared.Infrastructure.OpenApi;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Formatting.Compact;
using Scalar.AspNetCore;

Activity.DefaultIdFormat = ActivityIdFormat.W3C;
Activity.ForceDefaultIdFormat = true;

var builder = WebApplication.CreateBuilder(args);
var fileLoggingOptions = builder.Configuration.GetSection(FileLoggingOptions.SectionName).Get<FileLoggingOptions>()
    ?? new FileLoggingOptions();

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            new CompactJsonFormatter(),
            fileLoggingOptions.GetRollingFilePath(),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: fileLoggingOptions.RetainedFileCountLimit,
            fileSizeLimitBytes: fileLoggingOptions.FileSizeLimitBytes,
            rollOnFileSizeLimit: true,
            shared: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1));
});

builder.WebHost.ConfigureDriveHost(builder.Configuration);

builder.Services
    .AddInfrastructure(builder.Configuration, builder.Environment)
    .AddAdminModule(builder.Configuration)
    .AddAuthModule(builder.Configuration)
    .AddDriveModule(builder.Configuration)
    .AddProcessingModule(builder.Configuration)
    .AddSearchChatModule(builder.Configuration);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<BearerSecurityRequirementTransformer>();
});

var app = builder.Build();

await app.ApplyPendingMigrationsAsync();

app.UseSerilogRequestLogging();
app.UseInfrastructure();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var openApiEnabled = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled");
if (openApiEnabled)
{
    app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
    app.MapScalarApiReference("/docs", options => options
        .WithTitle("Amanah Drive API")
        .WithOpenApiRoutePattern("/openapi/{documentName}.json")
        .AddPreferredSecuritySchemes("Bearer")
        .EnablePersistentAuthentication()).AllowAnonymous();
}

static async Task<IResult> ReadinessAsync(HealthCheckService healthCheckService, CancellationToken cancellationToken)
{
    var report = await healthCheckService.CheckHealthAsync(
        registration => registration.Tags.Contains("ready"),
        cancellationToken);
    var response = new HealthResponse(report.Status.ToString());
    return report.Status == HealthStatus.Healthy
        ? Results.Ok(response)
        : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
}

app.MapGet("/health/live", () => Results.Ok(new HealthResponse(HealthStatus.Healthy.ToString())))
    .WithTags("Health")
    .WithSummary("Return process liveness status.")
    .Produces<HealthResponse>(StatusCodes.Status200OK);

app.MapGet("/health/ready", ReadinessAsync)
    .WithTags("Health")
    .WithSummary("Return dependency readiness status.")
    .Produces<HealthResponse>(StatusCodes.Status200OK)
    .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

app.MapGet("/health", ReadinessAsync)
    .WithTags("Health")
    .WithSummary("Readiness alias kept for backward compatibility.")
    .Produces<HealthResponse>(StatusCodes.Status200OK)
    .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);
app.MapAdminModule();
app.MapAuthModule();
app.MapDriveModule();
app.MapProcessingModule();
app.MapSearchChatModule();

app.Run();

public partial class Program;

public sealed record HealthResponse(string Status);
