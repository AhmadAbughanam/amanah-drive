using AmanahDrive.Api.Modules.Auth;
using AmanahDrive.Api.Modules.Drive;
using AmanahDrive.Api.Modules.Processing;
using AmanahDrive.Api.Modules.SearchChat;
using AmanahDrive.Api.Shared.Infrastructure;
using AmanahDrive.Api.Shared.Infrastructure.OpenApi;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.WebHost.ConfigureDriveHost(builder.Configuration);

builder.Services
    .AddInfrastructure(builder.Configuration, builder.Environment)
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

app.MapGet("/health", async (HealthCheckService healthCheckService, CancellationToken cancellationToken) =>
{
    var report = await healthCheckService.CheckHealthAsync(cancellationToken);
    var response = new HealthResponse(report.Status.ToString());
    return report.Status == HealthStatus.Healthy
        ? Results.Ok(response)
        : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
})
    .WithTags("Health")
    .WithSummary("Return API health status.")
    .Produces<HealthResponse>(StatusCodes.Status200OK)
    .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);
app.MapAuthModule();
app.MapDriveModule();
app.MapProcessingModule();
app.MapSearchChatModule();

app.Run();

public partial class Program;

public sealed record HealthResponse(string Status);
