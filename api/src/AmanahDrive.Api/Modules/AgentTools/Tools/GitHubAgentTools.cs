using System.Text;
using AmanahDrive.Api.Shared.Infrastructure.GitHub;

namespace AmanahDrive.Api.Modules.AgentTools.Tools;

public sealed record ListGitHubDirectoryRequest(string Owner, string Repo, string? Path = null, string? Ref = null);

public sealed record GitHubDirectoryResponse(string Owner, string Repo, string Path, string? Ref, IReadOnlyCollection<GitHubDirectoryEntry> Entries);

public sealed class ListGitHubDirectoryTool(IGitHubClient gitHubClient) : IAgentTool<ListGitHubDirectoryRequest, GitHubDirectoryResponse>
{
    public string Name => "list_github_directory";

    public bool RequiresApproval => false;

    public async Task<AgentToolResult<GitHubDirectoryResponse>> ExecuteAsync(AgentToolContext context, ListGitHubDirectoryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Owner) || string.IsNullOrWhiteSpace(request.Repo))
        {
            return AgentToolResult<GitHubDirectoryResponse>.Invalid("GitHub owner and repository are required.");
        }

        var path = NormalizePath(request.Path);
        var result = await gitHubClient.ListDirectoryAsync(request.Owner.Trim(), request.Repo.Trim(), path, NormalizeRef(request.Ref), cancellationToken);
        return result.Status switch
        {
            GitHubClientStatus.Success when result.Value is not null => AgentToolResult<GitHubDirectoryResponse>.Success(new GitHubDirectoryResponse(request.Owner.Trim(), request.Repo.Trim(), path, NormalizeRef(request.Ref), result.Value)),
            GitHubClientStatus.NotFound => AgentToolResult<GitHubDirectoryResponse>.NotFound(),
            _ => AgentToolResult<GitHubDirectoryResponse>.Invalid(result.ErrorMessage ?? "GitHub directory request failed.")
        };
    }

    private static string NormalizePath(string? path) => (path ?? string.Empty).Trim('/');

    private static string? NormalizeRef(string? gitReference) => string.IsNullOrWhiteSpace(gitReference) ? null : gitReference.Trim();
}

public sealed record ReadGitHubFileRequest(string Owner, string Repo, string Path, string? Ref = null);

public sealed record GitHubFileTextResponse(
    string Owner,
    string Repo,
    string Path,
    string? Ref,
    string Text,
    bool IsTruncated,
    int TotalCharacterCount,
    int ReturnedCharacterCount);

public sealed class ReadGitHubFileTool(IGitHubClient gitHubClient) : IAgentTool<ReadGitHubFileRequest, GitHubFileTextResponse>
{
    private const int MaximumReturnedCharacters = 100_000;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public string Name => "read_github_file";

    public bool RequiresApproval => false;

    public async Task<AgentToolResult<GitHubFileTextResponse>> ExecuteAsync(AgentToolContext context, ReadGitHubFileRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Owner) || string.IsNullOrWhiteSpace(request.Repo) || string.IsNullOrWhiteSpace(request.Path))
        {
            return AgentToolResult<GitHubFileTextResponse>.Invalid("GitHub owner, repository, and file path are required.");
        }

        var path = request.Path.Trim('/');
        var gitReference = string.IsNullOrWhiteSpace(request.Ref) ? null : request.Ref.Trim();
        var result = await gitHubClient.ReadFileAsync(request.Owner.Trim(), request.Repo.Trim(), path, gitReference, cancellationToken);
        if (result.Status == GitHubClientStatus.NotFound) return AgentToolResult<GitHubFileTextResponse>.NotFound();
        if (result.Status != GitHubClientStatus.Success || result.Value is null)
        {
            return AgentToolResult<GitHubFileTextResponse>.Invalid(result.ErrorMessage ?? "GitHub file request failed.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(result.Value.Content);
        }
        catch (FormatException)
        {
            return AgentToolResult<GitHubFileTextResponse>.Invalid("GitHub returned invalid base64 file content.");
        }

        if (IsBinary(bytes))
        {
            return AgentToolResult<GitHubFileTextResponse>.Invalid("The requested GitHub file appears to be binary and cannot be returned as text.");
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return AgentToolResult<GitHubFileTextResponse>.Invalid("The requested GitHub file is not valid UTF-8 text.");
        }

        var returnedText = text.Length <= MaximumReturnedCharacters ? text : text[..MaximumReturnedCharacters];
        return AgentToolResult<GitHubFileTextResponse>.Success(new GitHubFileTextResponse(
            request.Owner.Trim(),
            request.Repo.Trim(),
            path,
            gitReference,
            returnedText,
            text.Length > MaximumReturnedCharacters,
            text.Length,
            returnedText.Length));
    }

    private static bool IsBinary(IReadOnlyList<byte> bytes)
    {
        if (bytes.Contains((byte)0)) return true;
        if (bytes.Count == 0) return false;

        var controlCharacterCount = bytes.Count(value => value < 32 && value is not (9 or 10 or 13));
        return controlCharacterCount * 100 > bytes.Count;
    }
}
