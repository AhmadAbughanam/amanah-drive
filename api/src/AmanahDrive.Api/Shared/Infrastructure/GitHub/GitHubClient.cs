using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Shared.Infrastructure.GitHub;

public sealed class GitHubClient(HttpClient httpClient, IOptions<GitHubOptions> options) : IGitHubClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _readToken = options.Value.ReadToken;

    public async Task<GitHubClientResult<IReadOnlyCollection<GitHubDirectoryEntry>>> ListDirectoryAsync(
        string owner,
        string repository,
        string? path,
        string? gitReference,
        CancellationToken cancellationToken)
    {
        var response = await GetContentsAsync(owner, repository, path, gitReference, cancellationToken);
        if (response.Status != GitHubClientStatus.Success || response.Value is null)
        {
            return new GitHubClientResult<IReadOnlyCollection<GitHubDirectoryEntry>>(response.Status, ErrorMessage: response.ErrorMessage);
        }

        using var document = response.Value;
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return GitHubClientResult<IReadOnlyCollection<GitHubDirectoryEntry>>.Invalid("The requested GitHub path is a file, not a directory.");
        }

        var entries = JsonSerializer.Deserialize<List<GitHubDirectoryEntry>>(document.RootElement.GetRawText(), JsonOptions);
        return entries is null
            ? GitHubClientResult<IReadOnlyCollection<GitHubDirectoryEntry>>.Invalid("GitHub returned an empty directory response.")
            : GitHubClientResult<IReadOnlyCollection<GitHubDirectoryEntry>>.Success(entries);
    }

    public async Task<GitHubClientResult<GitHubFileContent>> ReadFileAsync(
        string owner,
        string repository,
        string path,
        string? gitReference,
        CancellationToken cancellationToken)
    {
        var response = await GetContentsAsync(owner, repository, path, gitReference, cancellationToken);
        if (response.Status != GitHubClientStatus.Success || response.Value is null)
        {
            return new GitHubClientResult<GitHubFileContent>(response.Status, ErrorMessage: response.ErrorMessage);
        }

        using var document = response.Value;
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            return GitHubClientResult<GitHubFileContent>.Invalid("The requested GitHub path is a directory, not a file.");
        }

        var file = JsonSerializer.Deserialize<GitHubFileContent>(document.RootElement.GetRawText(), JsonOptions);
        if (file is null || !string.Equals(file.Type, "file", StringComparison.OrdinalIgnoreCase))
        {
            return GitHubClientResult<GitHubFileContent>.Invalid("The requested GitHub path is not a regular file.");
        }

        if (!string.Equals(file.Encoding, "base64", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(file.Content))
        {
            return GitHubClientResult<GitHubFileContent>.Invalid("GitHub did not return base64 file content for this path.");
        }

        return GitHubClientResult<GitHubFileContent>.Success(file);
    }

    private async Task<GitHubClientResult<JsonDocument>> GetContentsAsync(
        string owner,
        string repository,
        string? path,
        string? gitReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_readToken))
        {
            return GitHubClientResult<JsonDocument>.Invalid(
                "GitHub integration is not configured. Set GITHUB_READ_TOKEN to enable this tool.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildContentsUri(owner, repository, path, gitReference));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.ParseAdd("AmanahDrive/1.0");
        // Keep this token even for public repositories: authenticated reads get GitHub's
        // 5,000 requests/hour limit instead of the unauthenticated 60 requests/hour limit.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _readToken);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return GitHubClientResult<JsonDocument>.Invalid($"GitHub request failed: {exception.Message}");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    return GitHubClientResult<JsonDocument>.Success(JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken)));
                }
                catch (JsonException)
                {
                    return GitHubClientResult<JsonDocument>.Invalid("GitHub returned invalid JSON.");
                }
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return GitHubClientResult<JsonDocument>.NotFound();
            }

            var detail = await ReadErrorDetailAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var isRateLimited =
                    (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.FirstOrDefault() == "0") ||
                    detail.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
                return GitHubClientResult<JsonDocument>.Invalid(isRateLimited
                    ? $"GitHub rate limit was reached. {detail}"
                    : $"GitHub denied access to this repository. {detail}");
            }

            return GitHubClientResult<JsonDocument>.Invalid($"GitHub returned {(int)response.StatusCode}: {detail}");
        }
    }

    private static Uri BuildContentsUri(string owner, string repository, string? path, string? gitReference)
    {
        var segments = new[] { "repos", owner, repository, "contents" }
            .Concat((path ?? string.Empty).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries))
            .Select(Uri.EscapeDataString);
        var relativePath = string.Join('/', segments);
        return string.IsNullOrWhiteSpace(gitReference)
            ? new Uri(relativePath, UriKind.Relative)
            : new Uri($"{relativePath}?ref={Uri.EscapeDataString(gitReference)}", UriKind.Relative);
    }

    private static async Task<string> ReadErrorDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body)) return "No error detail was returned.";

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String
                ? message.GetString() ?? body
                : body;
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
