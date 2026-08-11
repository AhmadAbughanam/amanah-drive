using AmanahDrive.Api.Shared.Infrastructure.Ai;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Modules.Processing;

public sealed class DocumentProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AiServiceOptions> options,
    ILogger<DocumentProcessingWorker> logger) : BackgroundService
{
    private readonly AiServiceOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<ProcessingJobRunner>();
                var processed = await runner.ProcessNextPendingJobAsync(stoppingToken);

                if (!processed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.WorkerPollSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Document processing worker loop failed");
                await Task.Delay(TimeSpan.FromSeconds(_options.WorkerPollSeconds), stoppingToken);
            }
        }
    }
}
