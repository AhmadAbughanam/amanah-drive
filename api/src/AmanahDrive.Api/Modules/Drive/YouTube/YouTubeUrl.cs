using System.Text.RegularExpressions;

namespace AmanahDrive.Api.Modules.Drive.YouTube;

public static partial class YouTubeUrl
{
    public static bool TryNormalize(string? value, out string canonicalUrl)
    {
        canonicalUrl = string.Empty;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        var videoId = host is "youtu.be" or "www.youtu.be"
            ? uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            : host is "youtube.com" or "www.youtube.com" or "m.youtube.com"
                ? GetYouTubeVideoId(uri)
                : null;

        if (videoId is null || !VideoIdRegex().IsMatch(videoId))
        {
            return false;
        }

        canonicalUrl = $"https://www.youtube.com/watch?v={videoId}";
        return true;
    }

    private static string? GetYouTubeVideoId(Uri uri)
    {
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 && segments[0] is "shorts" or "embed" or "live")
        {
            return segments[1];
        }

        if (!uri.AbsolutePath.Equals("/watch", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .FirstOrDefault(pair => pair.Length == 2 && pair[0].Equals("v", StringComparison.OrdinalIgnoreCase))?
            .ElementAtOrDefault(1) is { } videoId
            ? Uri.UnescapeDataString(videoId)
            : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant)]
    private static partial Regex VideoIdRegex();
}
