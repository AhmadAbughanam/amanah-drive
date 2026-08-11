using AmanahDrive.Api.Modules.Auth;
using AmanahDrive.Api.Modules.Drive;
using AmanahDrive.Api.Modules.Processing;
using AmanahDrive.Api.Modules.SearchChat;
using AmanahDrive.Api.Shared.Infrastructure;
using Serilog;

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

var app = builder.Build();

await app.ApplyPendingMigrationsAsync();

app.UseSerilogRequestLogging();
app.UseInfrastructure();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapAuthModule();
app.MapDriveModule();
app.MapProcessingModule();
app.MapSearchChatModule();

app.Run();

public partial class Program;
