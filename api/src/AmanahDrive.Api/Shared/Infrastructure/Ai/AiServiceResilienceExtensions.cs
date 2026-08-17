using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace AmanahDrive.Api.Shared.Infrastructure.Ai;

public static class AiServiceResilienceExtensions
{
    public static IHttpClientBuilder AddAiServiceResilience(
        this IHttpClientBuilder clientBuilder,
        AiServiceOptions options)
    {
        clientBuilder.AddResilienceHandler("ai-service", pipeline =>
        {
            pipeline
                .AddTimeout(TimeSpan.FromSeconds(options.TotalTimeoutSeconds))
                .AddRetry(new HttpRetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>()
                        .HandleResult(response => response.StatusCode >= HttpStatusCode.InternalServerError),
                    MaxRetryAttempts = options.RetryMaxAttempts,
                    Delay = TimeSpan.FromMilliseconds(options.RetryBaseDelayMilliseconds),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                })
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>()
                        .HandleResult(response => response.StatusCode >= HttpStatusCode.InternalServerError),
                    FailureRatio = 1.0,
                    MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                    SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreakerSamplingSeconds),
                    BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerBreakSeconds)
                })
                .AddTimeout(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds));
        });

        return clientBuilder;
    }
}
