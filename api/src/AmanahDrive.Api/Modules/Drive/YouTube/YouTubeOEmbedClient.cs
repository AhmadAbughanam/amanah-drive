using System.Net.Http.Json;
using System.Text.Json;

namespace AmanahDrive.Api.Modules.Drive.YouTube;

public sealed class YouTubeOEmbedClient(HttpClient httpClient) : IYouTubeOEmbedClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<YouTubeOEmbedResult> GetVideoAsync(string canonicalUrl, CancellationToken cancellationToken)
    {
        var requestUrl = $"oembed?url={Uri.EscapeDataString(canonicalUrl)}&format=json";
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(requestUrl, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return new YouTubeOEmbedResult(null, "YouTube video metadata could not be fetched. Try again shortly.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new YouTubeOEmbedResult(null, $"YouTube video could not be found or is unavailable ({(int)response.StatusCode}).");
            }

            var payload = await response.Content.ReadFromJsonAsync<YouTubeOEmbedPayload>(JsonOptions, cancellationToken);
            return string.IsNullOrWhiteSpace(payload?.Title)
                ? new YouTubeOEmbedResult(null, "YouTube did not return a video title.")
                : new YouTubeOEmbedResult(payload.Title.Trim());
        }
    }

    private sealed record YouTubeOEmbedPayload(string? Title);
}
