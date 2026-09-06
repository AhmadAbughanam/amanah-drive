using AmanahDrive.Api.Modules.Agent.Options;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Modules.Agent.Services;

public sealed class AgentRunWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AgentOptions> options,
    ILogger<AgentRunWorker> logger) : BackgroundService
{
    private readonly AgentOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAgentRunService>();
                var processed = await service.ProcessNextPendingRunAsync(stoppingToken);

                if (!processed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.WorkerPollSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Agent run worker loop failed");
                await Task.Delay(TimeSpan.FromSeconds(_options.WorkerPollSeconds), stoppingToken);
            }
        }
    }
}
