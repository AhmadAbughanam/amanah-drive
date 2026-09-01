using System.Net;
using System.Text;
using AmanahDrive.Api.Modules.Drive.YouTube;

namespace AmanahDrive.Api.Tests;

public sealed class YouTubeOEmbedClientTests
{
    [Fact]
    public async Task GetVideoAsync_ReturnsTitleFromOEmbedResponse()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"title\":\"Example video\"}");
        var client = new YouTubeOEmbedClient(new HttpClient(handler) { BaseAddress = new Uri("https://www.youtube.com/") });

        var result = await client.GetVideoAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Example video", result.Title);
        Assert.Contains("oembed?url=", handler.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetVideoAsync_MapsUnavailableVideoToClearError()
    {
        var client = new YouTubeOEmbedClient(new HttpClient(new StubHandler(HttpStatusCode.NotFound, "not found")) { BaseAddress = new Uri("https://www.youtube.com/") });

        var result = await client.GetVideoAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("unavailable", result.ErrorMessage);
    }

    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    public void TryNormalize_AcceptsSupportedYouTubeUrls(string value)
    {
        Assert.True(YouTubeUrl.TryNormalize(value, out var normalized));
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", normalized);
    }

    [Fact]
    public void TryNormalize_RejectsNonYouTubeUrls()
    {
        Assert.False(YouTubeUrl.TryNormalize("https://example.com/watch?v=dQw4w9WgXcQ", out _));
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
