using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace AmanahDrive.Api.Shared.Infrastructure.Ai;

public sealed class AiProcessingClient(HttpClient httpClient, IOptions<AiServiceOptions> options) : IAiProcessingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AiServiceOptions _options = options.Value;

    public async Task<ExtractResponse> ExtractAsync(string fileName, string contentType, Stream fileStream, CancellationToken cancellationToken)
    {
        using var replayableContent = new MemoryStream();
        await fileStream.CopyToAsync(replayableContent, cancellationToken);

        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(replayableContent.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/extract")
        {
            Content = form
        };

        AddServiceToken(request);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadJsonAsync<ExtractResponse>(response, cancellationToken);
    }

    public async Task<ChunkResponse> ChunkAsync(string text, int chunkSize, int overlap, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/chunk")
        {
            Content = JsonContent.Create(new
            {
                text,
                chunkSize,
                overlap
            }, options: JsonOptions)
        };

        AddServiceToken(request);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadJsonAsync<ChunkResponse>(response, cancellationToken);
    }

    public async Task<EmbedResponse> EmbedAsync(IReadOnlyCollection<string> texts, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/embed")
        {
            Content = JsonContent.Create(new
            {
                texts
            }, options: JsonOptions)
        };

        AddServiceToken(request);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadJsonAsync<EmbedResponse>(response, cancellationToken);
    }

    public async Task<RagAnswerResponse> AnswerAsync(RagAnswerRequest requestBody, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/rag/answer")
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions)
        };

        AddServiceToken(request);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadJsonAsync<RagAnswerResponse>(response, cancellationToken);
    }

    private void AddServiceToken(HttpRequestMessage request) =>
        request.Headers.Add("X-Service-Token", _options.ServiceToken);

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (BrokenCircuitException exception)
        {
            throw new AiServiceException("AI service is temporarily unavailable because the resilience circuit is open.", exception);
        }
        catch (TimeoutRejectedException exception)
        {
            throw new AiServiceException("AI service request timed out after exhausting its resilience policy.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AiServiceException("AI service request failed after exhausting its resilience policy.", exception);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new AiServiceException($"AI service returned {(int)response.StatusCode}: {body}");
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return value ?? throw new AiServiceException("AI service returned an empty response body.");
    }
}

public sealed class AiServiceException : Exception
{
    public AiServiceException(string message)
        : base(message)
    {
    }

    public AiServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
