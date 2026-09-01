using System.Net;
using System.Text;
using AmanahDrive.Api.Modules.AgentTools;
using AmanahDrive.Api.Modules.AgentTools.Tools;
using AmanahDrive.Api.Shared.Infrastructure.GitHub;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Tests;

public sealed class GitHubAgentToolTests
{
    [Fact]
    public async Task ListDirectory_ReturnsGitHubEntriesAndUsesRefAndRequiredHeaders()
    {
        var handler = new StubHandler(_ => JsonResponse("""
            [{"type":"file","name":"README.md","path":"docs/README.md","sha":"abc123","size":12}]
            """));
        var tool = new ListGitHubDirectoryTool(CreateClient(handler));

        var result = await tool.ExecuteAsync(new AgentToolContext(Guid.NewGuid()), new ListGitHubDirectoryRequest("octo", "demo", "docs", "main"), CancellationToken.None);

        Assert.Equal(AgentToolStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        var entry = Assert.Single(result.Value.Entries);
        Assert.Equal("README.md", entry.Name);
        Assert.Equal("https://api.github.com/repos/octo/demo/contents/docs?ref=main", handler.RequestUri?.ToString());
        Assert.Equal("application/vnd.github+json", handler.Accept);
        Assert.Equal("2022-11-28", handler.ApiVersion);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
    }

    [Fact]
    public async Task ReadFile_ReturnsDecodedText()
    {
        var handler = new StubHandler(_ => JsonResponse(FilePayload("README.md", "README.md", "hello from GitHub")));
        var tool = new ReadGitHubFileTool(CreateClient(handler));

        var result = await tool.ExecuteAsync(new AgentToolContext(Guid.NewGuid()), new ReadGitHubFileRequest("octo", "demo", "README.md"), CancellationToken.None);

        Assert.Equal(AgentToolStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal("hello from GitHub", result.Value.Text);
        Assert.False(result.Value.IsTruncated);
        Assert.Equal(17, result.Value.TotalCharacterCount);
    }

    [Fact]
    public async Task ReadFile_OverCharacterCap_ReturnsTruncationMetadata()
    {
        var contents = new string('x', 100_001);
        var handler = new StubHandler(_ => JsonResponse(FilePayload("large.txt", "large.txt", contents)));
        var tool = new ReadGitHubFileTool(CreateClient(handler));

        var result = await tool.ExecuteAsync(new AgentToolContext(Guid.NewGuid()), new ReadGitHubFileRequest("octo", "demo", "large.txt"), CancellationToken.None);

        Assert.Equal(AgentToolStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.IsTruncated);
        Assert.Equal(100_001, result.Value.TotalCharacterCount);
        Assert.Equal(100_000, result.Value.ReturnedCharacterCount);
        Assert.Equal(100_000, result.Value.Text.Length);
    }

    [Fact]
    public async Task ReadFile_NotFound_ReturnsNotFound()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"Not Found"}""")
        });
        var tool = new ReadGitHubFileTool(CreateClient(handler));

        var result = await tool.ExecuteAsync(new AgentToolContext(Guid.NewGuid()), new ReadGitHubFileRequest("octo", "demo", "missing.txt"), CancellationToken.None);

        Assert.Equal(AgentToolStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ReadFile_BinaryContent_IsRejected()
    {
        var handler = new StubHandler(_ => JsonResponse(FilePayload("image.bin", "image.bin", new byte[] { 0, 159, 146, 150 })));
        var tool = new ReadGitHubFileTool(CreateClient(handler));

        var result = await tool.ExecuteAsync(new AgentToolContext(Guid.NewGuid()), new ReadGitHubFileRequest("octo", "demo", "image.bin"), CancellationToken.None);

        Assert.Equal(AgentToolStatus.Invalid, result.Status);
        Assert.Contains("binary", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListDirectory_RateLimited_ReturnsSpecificMessage()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("""{"message":"API rate limit exceeded"}""")
            };
            response.Headers.Add("X-RateLimit-Remaining", "0");
            return response;
        });
        var tool = new ListGitHubDirectoryTool(CreateClient(handler));

        var result = await tool.ExecuteAsync(new AgentToolContext(Guid.NewGuid()), new ListGitHubDirectoryRequest("octo", "demo"), CancellationToken.None);

        Assert.Equal(AgentToolStatus.Invalid, result.Status);
        Assert.Contains("rate limit", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("API rate limit exceeded", result.ErrorMessage, StringComparison.Ordinal);
    }

    private static IGitHubClient CreateClient(HttpMessageHandler handler) => new GitHubClient(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") },
        Options.Create(new GitHubOptions { ReadToken = "tests-only-github-read-token" }));

    private static HttpResponseMessage JsonResponse(string payload) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };

    private static string FilePayload(string name, string path, string text) => FilePayload(name, path, Encoding.UTF8.GetBytes(text));

    private static string FilePayload(string name, string path, byte[] bytes) => $$"""
        {"type":"file","name":"{{name}}","path":"{{path}}","sha":"abc123","size":{{bytes.Length}},"encoding":"base64","content":"{{Convert.ToBase64String(bytes)}}"}
        """;

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? Accept { get; private set; }
        public string? ApiVersion { get; private set; }
        public string? AuthorizationScheme { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Accept = request.Headers.Accept.SingleOrDefault()?.MediaType;
            ApiVersion = request.Headers.TryGetValues("X-GitHub-Api-Version", out var values) ? values.SingleOrDefault() : null;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            return Task.FromResult(responseFactory(request));
        }
    }
}
