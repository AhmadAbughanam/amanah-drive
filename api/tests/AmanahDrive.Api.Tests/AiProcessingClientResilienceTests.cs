using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using AmanahDrive.Api.Shared.Infrastructure.Ai;
using AmanahDrive.Api.Shared.Infrastructure.Observability;
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
        var recorder = new CapturingUsageRecorder();
        var handler = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("invalid request")
        });
        await using var services = BuildServices(handler, RetryOptions(maxRetryAttempts: 2, minimumThroughput: 4), recorder);
        var client = services.GetRequiredService<IAiProcessingClient>();

        var exception = await Assert.ThrowsAsync<AiServiceException>(() => client.EmbedAsync(["test"], CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
        Assert.Contains("400", exception.Message, StringComparison.Ordinal);
        var usage = Assert.Single(recorder.Measurements);
        Assert.Equal("sentence-transformers/all-MiniLM-L6-v2", usage.Model);
        Assert.False(usage.Succeeded);
    }

    [Fact]
    public async Task EmbedAsync_RecordsMeasuredUsageAfterRetriesComplete()
    {
        var recorder = new CapturingUsageRecorder();
        var handler = new SequenceHandler(call => call == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : JsonResponse(new EmbedResponse(
                "test-model",
                3,
                [[1f, 2f, 3f]],
                new AiModelUsage("local", 4, 0))));
        await using var services = BuildServices(
            handler,
            RetryOptions(maxRetryAttempts: 1, minimumThroughput: 4),
            recorder);
        var client = services.GetRequiredService<IAiProcessingClient>();

        await client.EmbedAsync(["test"], CancellationToken.None);

        var usage = Assert.Single(recorder.Measurements);
        Assert.Equal("embed", usage.Operation);
        Assert.Equal("local", usage.Provider);
        Assert.Equal("test-model", usage.Model);
        Assert.Equal(4, usage.InputTokens);
        Assert.Equal(0, usage.OutputTokens);
        Assert.True(usage.Succeeded);
        Assert.True(usage.LatencyMilliseconds >= 0);
    }

    [Fact]
    public async Task ExtractYouTubeTranscriptAsync_PostsOnlyTheSourceUrlAndRecordsYouTubeUsage()
    {
        var recorder = new CapturingUsageRecorder();
        var handler = new CapturingHandler(JsonResponse(new YouTubeTranscriptResponse("caption text", 12)));
        await using var services = BuildServices(handler, RetryOptions(maxRetryAttempts: 1, minimumThroughput: 4), recorder);
        var client = services.GetRequiredService<IAiProcessingClient>();

        var result = await client.ExtractYouTubeTranscriptAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ", CancellationToken.None);

        Assert.Equal("caption text", result.Text);
        Assert.Equal("/youtube/transcript", handler.RequestUri!.AbsolutePath);
        Assert.Contains("dQw4w9WgXcQ", handler.Body);
        Assert.Equal("tests-only-service-token", handler.ServiceToken);
        var usage = Assert.Single(recorder.Measurements);
        Assert.Equal("extract.youtube", usage.Operation);
        Assert.Equal("youtube", usage.Provider);
    }

    private static ServiceProvider BuildServices(
        HttpMessageHandler handler,
        AiServiceOptions options,
        IAiUsageRecorder? recorder = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptions<AiServiceOptions>>(Options.Create(options));
        if (recorder is not null)
        {
            services.AddSingleton(recorder);
        }
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

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string Body { get; private set; } = string.Empty;
        public string? ServiceToken { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ServiceToken = request.Headers.GetValues("X-Service-Token").SingleOrDefault();
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }

    private sealed class CapturingUsageRecorder : IAiUsageRecorder
    {
        public List<AiUsageMeasurement> Measurements { get; } = [];

        public Task RecordAsync(AiUsageMeasurement measurement, CancellationToken cancellationToken)
        {
            Measurements.Add(measurement);
            return Task.CompletedTask;
        }
    }
}
