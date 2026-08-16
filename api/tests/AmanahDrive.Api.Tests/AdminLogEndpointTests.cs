using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace AmanahDrive.Api.Tests;

public sealed class AdminLogEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("amanah_drive_admin_log_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string _logDirectory = Path.Combine(Path.GetTempPath(), $"amanah-drive-log-tests-{Guid.NewGuid():N}");
    private AmanahDriveApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_logDirectory);
        await File.WriteAllLinesAsync(Path.Combine(_logDirectory, "api-fixture.clef"),
        [
            "{\"@t\":\"2026-08-16T10:00:00.0000000Z\",\"@mt\":\"viewer-marker first {Code}\",\"@l\":\"Warning\",\"Code\":\"alpha\"}",
            "{\"@t\":\"2026-08-16T10:01:00.0000000Z\",\"@mt\":\"viewer-marker second {Code}\",\"@l\":\"Warning\",\"Code\":\"beta\"}",
            "{\"@t\":\"2026-08-16T10:02:00.0000000Z\",\"@mt\":\"unrelated information\"}"
        ]);

        await _postgres.StartAsync();
        _factory = new AmanahDriveApiFactory(
            _postgres.GetConnectionString(),
            settings: new Dictionary<string, string?>
            {
                ["LoggingFiles:DirectoryPath"] = _logDirectory,
                ["LoggingFiles:DefaultPageSize"] = "1",
                ["LoggingFiles:MaxPageSize"] = "2"
            });
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
        try
        {
            Directory.Delete(_logDirectory, recursive: true);
        }
        catch (IOException)
        {
            // The process-wide Serilog sink can release its test file after the factory is disposed.
        }
    }

    [Fact]
    public async Task GetLogs_WithoutBearerToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/admin/logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLogs_FiltersByLevelAndTextAndPaginatesNewestFirst()
    {
        var client = await CreateAuthorizedClientAsync();

        var firstResponse = await client.GetAsync("/admin/logs?level=warning&search=viewer-marker&page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<LogPageDto>();
        Assert.NotNull(firstPage);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(1, firstPage.PageSize);
        Assert.True(firstPage.HasMore);
        var firstEntry = Assert.Single(firstPage.Entries);
        Assert.Equal("Warning", firstEntry.Level);
        Assert.Contains("second", firstEntry.Message);
        Assert.Equal("beta", firstEntry.Properties["Code"].GetString());

        var secondResponse = await client.GetAsync("/admin/logs?level=warning&search=viewer-marker&page=2&pageSize=1");
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<LogPageDto>();
        Assert.NotNull(secondPage);
        Assert.False(secondPage.HasMore);
        Assert.Contains("first", Assert.Single(secondPage.Entries).Message);
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register")
        {
            Content = JsonContent.Create(new
            {
                Email = TestUsers.Email,
                Password = TestUsers.Password
            })
        };
        request.Headers.Add("X-Bootstrap-Token", TestUsers.BootstrapToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(authResponse);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);
        return client;
    }

    private sealed record AuthResponseDto(string AccessToken);

    private sealed record LogPageDto(int Page, int PageSize, bool HasMore, IReadOnlyList<LogEntryDto> Entries);

    private sealed record LogEntryDto(
        DateTimeOffset Timestamp,
        string Level,
        string Message,
        string? Exception,
        IReadOnlyDictionary<string, JsonElement> Properties);
}
