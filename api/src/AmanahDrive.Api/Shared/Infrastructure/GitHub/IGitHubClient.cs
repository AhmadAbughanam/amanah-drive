namespace AmanahDrive.Api.Shared.Infrastructure.GitHub;

public interface IGitHubClient
{
    Task<GitHubClientResult<IReadOnlyCollection<GitHubDirectoryEntry>>> ListDirectoryAsync(
        string owner,
        string repository,
        string? path,
        string? gitReference,
        CancellationToken cancellationToken);

    Task<GitHubClientResult<GitHubFileContent>> ReadFileAsync(
        string owner,
        string repository,
        string path,
        string? gitReference,
        CancellationToken cancellationToken);
}

public enum GitHubClientStatus
{
    Success,
    NotFound,
    Invalid
}

public sealed record GitHubClientResult<T>(GitHubClientStatus Status, T? Value = default, string? ErrorMessage = null)
{
    public static GitHubClientResult<T> Success(T value) => new(GitHubClientStatus.Success, value);

    public static GitHubClientResult<T> NotFound() => new(GitHubClientStatus.NotFound);

    public static GitHubClientResult<T> Invalid(string message) => new(GitHubClientStatus.Invalid, default, message);
}

public sealed record GitHubDirectoryEntry(string Name, string Path, string Type, string Sha, long Size);

public sealed record GitHubFileContent(string Type, string Name, string Path, string Sha, long Size, string Encoding, string Content);
