namespace AmanahDrive.Api.Modules.Drive.YouTube;

public interface IYouTubeOEmbedClient
{
    Task<YouTubeOEmbedResult> GetVideoAsync(string canonicalUrl, CancellationToken cancellationToken);
}

public sealed record YouTubeOEmbedResult(string? Title, string? ErrorMessage = null)
{
    public bool IsSuccess => Title is not null;
}
