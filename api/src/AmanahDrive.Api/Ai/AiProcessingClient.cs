using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AmanahDrive.Api.Options;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Ai;

public sealed class AiProcessingClient(HttpClient httpClient, IOptions<AiServiceOptions> options) : IAiProcessingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AiServiceOptions _options = options.Value;

    public async Task<ExtractResponse> ExtractAsync(string fileName, string contentType, Stream fileStream, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/extract")
        {
            Content = form
        };

        AddServiceToken(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
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
        using var response = await httpClient.SendAsync(request, cancellationToken);
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
        using var response = await httpClient.SendAsync(request, cancellationToken);
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
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await ReadJsonAsync<RagAnswerResponse>(response, cancellationToken);
    }

    private void AddServiceToken(HttpRequestMessage request) =>
        request.Headers.Add("X-Service-Token", _options.ServiceToken);

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

public sealed class AiServiceException(string message) : Exception(message);
