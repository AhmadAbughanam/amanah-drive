using AmanahDrive.Api.Modules.Processing.Search;
using AmanahDrive.Api.Shared.Infrastructure.Ai;

namespace AmanahDrive.Api.Modules.Processing;

public static class ProcessingModule
{
    public static IServiceCollection AddProcessingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var aiServiceOptions = configuration.GetSection(AiServiceOptions.SectionName).Get<AiServiceOptions>() ?? new AiServiceOptions();

        services.AddScoped<ProcessingJobRunner>();
        services.AddScoped<IChunkSearchRepository, ChunkSearchRepository>();

        if (aiServiceOptions.WorkerEnabled)
        {
            services.AddHostedService<DocumentProcessingWorker>();
        }

        return services;
    }

    public static IEndpointRouteBuilder MapProcessingModule(this IEndpointRouteBuilder app) => app;
}
