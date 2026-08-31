using AmanahDrive.Api.Modules.Agent.Endpoints;
using AmanahDrive.Api.Modules.Agent.Options;
using AmanahDrive.Api.Modules.Agent.Services;

namespace AmanahDrive.Api.Modules.Agent;

public static class AgentModule
{
    public static IServiceCollection AddAgentModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AgentOptions>()
            .Bind(configuration.GetSection(AgentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IAgentRunService, AgentRunService>();
        return services;
    }

    public static IEndpointRouteBuilder MapAgentModule(this IEndpointRouteBuilder app)
    {
        app.MapAgentEndpoints();
        return app;
    }
}
