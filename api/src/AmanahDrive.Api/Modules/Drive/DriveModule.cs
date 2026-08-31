using AmanahDrive.Api.Modules.Drive.Endpoints;
using AmanahDrive.Api.Modules.Drive.Options;
using AmanahDrive.Api.Modules.Drive.Services;
using AmanahDrive.Api.Modules.Drive.Storage;
using Microsoft.AspNetCore.Http.Features;

namespace AmanahDrive.Api.Modules.Drive;

public static class DriveModule
{
    public static void ConfigureDriveHost(this ConfigureWebHostBuilder webHost, IConfiguration configuration)
    {
        var driveOptions = configuration.GetSection(DriveOptions.SectionName).Get<DriveOptions>() ?? new DriveOptions();
        webHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = driveOptions.MaxFileSizeBytes + 1024 * 1024;
        });
    }

    public static IServiceCollection AddDriveModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DriveOptions>()
            .Bind(configuration.GetSection(DriveOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.MaxFileSizeBytes > 0, "Drive:MaxFileSizeBytes must be greater than zero.")
            .Validate(options => options.AllowedContentTypes.Length > 0, "Drive:AllowedContentTypes must contain at least one content type.")
            .Validate(options => options.DefaultPageSize <= options.MaxPageSize, "Drive:DefaultPageSize must be less than or equal to Drive:MaxPageSize.")
            .ValidateOnStart();

        var driveOptions = configuration.GetSection(DriveOptions.SectionName).Get<DriveOptions>() ?? new DriveOptions();
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = driveOptions.MaxFileSizeBytes + 1024 * 1024;
        });

        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IDriveService, DriveService>();
        return services;
    }

    public static IEndpointRouteBuilder MapDriveModule(this IEndpointRouteBuilder app)
    {
        app.MapDriveEndpoints();
        return app;
    }
}
