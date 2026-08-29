using AmanahDrive.Api.Modules.Admin.Models;
using AmanahDrive.Api.Modules.Admin.Options;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using AmanahDrive.Api.Shared.Infrastructure.Observability;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Modules.Admin.Observability;

public sealed class AiUsageRecorder(
    IServiceScopeFactory scopeFactory,
    IOptions<AiPricingOptions> pricingOptions,
    ILogger<AiUsageRecorder> logger) : IAiUsageRecorder
{
    private readonly AiPricingOptions _pricingOptions = pricingOptions.Value;

    public async Task RecordAsync(AiUsageMeasurement measurement, CancellationToken cancellationToken)
    {
        var estimatedCost = EstimateCost(measurement);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
            await dbContext.AiUsageRecords.AddAsync(new AiUsageRecord
            {
                Id = Guid.NewGuid(),
                Provider = measurement.Provider,
                Model = measurement.Model,
                Operation = measurement.Operation,
                InputTokens = measurement.InputTokens,
                OutputTokens = measurement.OutputTokens,
                LatencyMilliseconds = measurement.LatencyMilliseconds,
                Succeeded = measurement.Succeeded,
                EstimatedCostUsd = estimatedCost,
                ErrorType = measurement.ErrorType,
                OccurredAt = measurement.OccurredAt
            }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.Log(
                measurement.Succeeded ? LogLevel.Information : LogLevel.Warning,
                "AI operation {Operation} via {Provider} model {Model} completed in {LatencyMilliseconds} ms with {InputTokens} input tokens, {OutputTokens} output tokens, estimated cost {EstimatedCostUsd}, success {Succeeded} {Category}",
                measurement.Operation,
                measurement.Provider,
                measurement.Model ?? "n/a",
                measurement.LatencyMilliseconds,
                measurement.InputTokens,
                measurement.OutputTokens,
                estimatedCost,
                measurement.Succeeded,
                "AI");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to persist AI usage for operation {Operation}; the primary operation is unaffected",
                measurement.Operation);
        }
    }

    private decimal? EstimateCost(AiUsageMeasurement measurement)
    {
        if (measurement.Provider.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        if (measurement.Model is null || measurement.InputTokens is null || measurement.OutputTokens is null)
        {
            return null;
        }

        var price = _pricingOptions.Models.FirstOrDefault(candidate =>
            candidate.Enabled &&
            candidate.Provider.Equals(measurement.Provider, StringComparison.OrdinalIgnoreCase) &&
            candidate.Model.Equals(measurement.Model, StringComparison.OrdinalIgnoreCase));
        if (price is null)
        {
            return null;
        }

        return (measurement.InputTokens.Value / 1_000_000m * price.InputUsdPerMillionTokens) +
               (measurement.OutputTokens.Value / 1_000_000m * price.OutputUsdPerMillionTokens);
    }
}
