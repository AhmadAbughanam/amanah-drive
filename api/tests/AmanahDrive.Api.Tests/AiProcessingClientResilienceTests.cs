using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using AmanahDrive.Api.Shared.Infrastructure.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Tests;

public sealed class AiProcessingClientResilienceTests
{
    [Fact]
    public async Task EmbedAsync_WhenTransientFailuresRecover_RetriesAndReturnsResponse()
    {
        var handler = new SequenceHandler(call => call <= 2
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : JsonResponse(new EmbedResponse("test-model", 3, [[1f, 2f, 3f]])));
        await using var services = BuildServices(handler, RetryOptions(maxRetryAttempts: 2, minimumThroughput: 4));
        var client = services.GetRequiredService<IAiProcessingClient>();

        var result = await client.EmbedAsync(["test"], CancellationToken.None);

        Assert.Equal(3, handler.CallCount);
        Assert.Equal("test-model", result.Model);
        Assert.Equal([1f, 2f, 3f], Assert.Single(result.Embeddings));
    }

    [Fact]
    public async Task EmbedAsync_WhenFailuresContinue_OpensCircuitAndSubsequentCallFailsFast()
    {
        var handler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        await using var services = BuildServices(handler, RetryOptions(maxRetryAttempts: 1, minimumThroughput: 2));
        var client = services.GetRequiredService<IAiProcessingClient>();

        await Assert.ThrowsAsync<AiServiceException>(() => client.EmbedAsync(["first"], CancellationToken.None));
        Assert.Equal(2, handler.CallCount);

        var stopwatch = Stopwatch.StartNew();
        var exception = await Assert.ThrowsAsync<AiServiceException>(() => client.EmbedAsync(["second"], CancellationToken.None));
        stopwatch.Stop();

        Assert.Equal(2, handler.CallCount);
        Assert.Contains("circuit is open", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500), $"Open-circuit call took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task EmbedAsync_WhenResponseIsClientError_DoesNotRetry()
    {
        var handler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("invalid request")
        });
        await using var services = BuildServices(handler, RetryOptions(maxRetryAttempts: 2, minimumThroughput: 4));
        var client = services.GetRequiredService<IAiProcessingClient>();

        var exception = await Assert.ThrowsAsync<AiServiceException>(() => client.EmbedAsync(["test"], CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
        Assert.Contains("400", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildServices(HttpMessageHandler handler, AiServiceOptions options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptions<AiServiceOptions>>(Options.Create(options));
        services
            .AddHttpClient<IAiProcessingClient, AiProcessingClient>(client => client.BaseAddress = new Uri(options.BaseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddAiServiceResilience(options);
        return services.BuildServiceProvider();
    }

    private static AiServiceOptions RetryOptions(int maxRetryAttempts, int minimumThroughput) => new()
    {
        BaseUrl = "http://ai-service.test",
        ServiceToken = "tests-only-service-token",
        RetryMaxAttempts = maxRetryAttempts,
        RetryBaseDelayMilliseconds = 1,
        AttemptTimeoutSeconds = 1,
        TotalTimeoutSeconds = 5,
        CircuitBreakerMinimumThroughput = minimumThroughput,
        CircuitBreakerSamplingSeconds = 10,
        CircuitBreakerBreakSeconds = 5
    };

    private static HttpResponseMessage JsonResponse<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(value)
    };

    private sealed class SequenceHandler(Func<int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _callCount);
            return Task.FromResult(responseFactory(call));
        }
    }
}
