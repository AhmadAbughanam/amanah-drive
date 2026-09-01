namespace AmanahDrive.Api.Shared.Infrastructure.GitHub;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    // Deliberately optional: the GitHub read tool is an add-on agent capability, not core
    // infrastructure like the database or JWT signing key. Leaving this unset must not prevent
    // the rest of the application (Auth, Drive, Search) from starting - the two GitHub tools
    // just report themselves as unconfigured when called instead.
    public string ReadToken { get; init; } = string.Empty;

    public bool IsConfigured => ReadToken.Length >= 20;
}
