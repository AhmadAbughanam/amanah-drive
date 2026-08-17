using AmanahDrive.Api.Modules.Admin.Activity;
using AmanahDrive.Api.Modules.Admin.Endpoints;
using AmanahDrive.Api.Modules.Admin.Logging;
using AmanahDrive.Api.Modules.Admin.Options;
using AmanahDrive.Api.Modules.Drive.Events;
using AmanahDrive.Api.Modules.Processing.Events;
using AmanahDrive.Api.Modules.SearchChat.Events;
using AmanahDrive.Api.Shared.DomainEvents;
using AmanahDrive.Api.Shared.Infrastructure.Logging;

namespace AmanahDrive.Api.Modules.Admin;

public static class AdminModule
{
    public static IServiceCollection AddAdminModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FileLoggingOptions>()
            .Bind(configuration.GetSection(FileLoggingOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.DefaultPageSize <= options.MaxPageSize, "LoggingFiles:DefaultPageSize must be less than or equal to LoggingFiles:MaxPageSize.")
            .Validate(options => Path.GetFileName(options.FileNamePrefix) == options.FileNamePrefix, "LoggingFiles:FileNamePrefix must not contain a path.")
            .ValidateOnStart();

        services.AddOptions<ActivityOptions>()
            .Bind(configuration.GetSection(ActivityOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.DefaultPageSize <= options.MaxPageSize, "AdminActivity:DefaultPageSize must be less than or equal to AdminActivity:MaxPageSize.")
            .ValidateOnStart();

        services.AddSingleton<ILogReader, CompactJsonLogReader>();
        services.AddScoped<ActivityEventHandler>();
        services.AddScoped<IDomainEventHandler<FileUploadedEvent>>(services => services.GetRequiredService<ActivityEventHandler>());
        services.AddScoped<IDomainEventHandler<ProcessingCompletedEvent>>(services => services.GetRequiredService<ActivityEventHandler>());
        services.AddScoped<IDomainEventHandler<ProcessingFailedEvent>>(services => services.GetRequiredService<ActivityEventHandler>());
        services.AddScoped<IDomainEventHandler<ChatAnsweredEvent>>(services => services.GetRequiredService<ActivityEventHandler>());
        return services;
    }

    public static IEndpointRouteBuilder MapAdminModule(this IEndpointRouteBuilder app)
    {
        app.MapAdminEndpoints();
        return app;
    }
}
