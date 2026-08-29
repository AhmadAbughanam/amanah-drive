using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AmanahDrive.Api.Modules.Admin.Activity;
using AmanahDrive.Api.Modules.Admin.Models;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
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
        var now = DateTimeOffset.UtcNow;
        await File.WriteAllLinesAsync(Path.Combine(_logDirectory, "api-fixture.clef"),
        [
            "{\"@t\":\"2026-08-16T10:00:00.0000000Z\",\"@mt\":\"viewer-marker first {Code}\",\"@l\":\"Warning\",\"Code\":\"alpha\"}",
            "{\"@t\":\"2026-08-16T10:01:00.0000000Z\",\"@mt\":\"viewer-marker second {Code}\",\"@l\":\"Warning\",\"Code\":\"beta\",\"AccessToken\":\"must-not-render\",\"Nested\":{\"Password\":\"must-not-render\"}}",
            "{\"@t\":\"2026-08-16T10:02:00.0000000Z\",\"@mt\":\"unrelated information\"}",
            Clef(now.AddMinutes(-12), "HTTP GET /drive/folders responded 200 in 12.5 ms", "Information", new()
            {
                ["SourceContext"] = "Serilog.AspNetCore.RequestLoggingMiddleware",
                ["RequestMethod"] = "GET",
                ["RequestPath"] = "/drive/folders",
                ["StatusCode"] = 200,
                ["Elapsed"] = 12.5
            }),
            Clef(now.AddMinutes(-8), "HTTP POST /chat responded 500 in 87.5 ms", "Error", new()
            {
                ["SourceContext"] = "Serilog.AspNetCore.RequestLoggingMiddleware",
                ["RequestMethod"] = "POST",
                ["RequestPath"] = "/chat",
                ["StatusCode"] = 500,
                ["Elapsed"] = 87.5,
                ["@x"] = "System.InvalidOperationException: generated test failure"
            }),
            Clef(now.AddMinutes(-4), "Admin login failed", "Warning", new()
            {
                ["SourceContext"] = "AmanahDrive.Api.Modules.Auth.AuthService",
                ["Category"] = "Security",
                ["SecurityEvent"] = "LoginFailed"
            })
        ]);

        await _postgres.StartAsync();
        _factory = new AmanahDriveApiFactory(
            _postgres.GetConnectionString(),
            settings: new Dictionary<string, string?>
            {
                ["LoggingFiles:DirectoryPath"] = _logDirectory,
                ["LoggingFiles:DefaultPageSize"] = "1",
                ["LoggingFiles:MaxPageSize"] = "2",
                ["AdminActivity:DefaultPageSize"] = "1",
                ["AdminActivity:MaxPageSize"] = "2"
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
        Assert.Equal("[REDACTED]", firstEntry.Properties["AccessToken"].GetString());
        Assert.Equal("[REDACTED]", firstEntry.Properties["Nested"].GetProperty("Password").GetString());

        var secondResponse = await client.GetAsync("/admin/logs?level=warning&search=viewer-marker&page=2&pageSize=1");
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<LogPageDto>();
        Assert.NotNull(secondPage);
        Assert.False(secondPage.HasMore);
        Assert.Contains("first", Assert.Single(secondPage.Entries).Message);
    }

    [Fact]
    public async Task GetActivity_WithoutBearerToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/admin/activity");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetObservability_WithoutBearerToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/admin/observability");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetObservability_ReturnsRealLogAndAiUsageAggregates()
    {
        var client = await CreateAuthorizedClientAsync();
        await SeedAiUsageAsync();

        var response = await client.GetAsync("/admin/observability?range=24h");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<ObservabilitySnapshotDto>();
        Assert.NotNull(snapshot);
        Assert.Equal("24h", snapshot.Range);
        Assert.True(snapshot.Stats.RequestsToday >= 2);
        Assert.True(snapshot.Stats.ErrorRatePercent > 0);
        Assert.True(snapshot.Stats.AverageLatencyMilliseconds > 0);
        Assert.Equal(0.00123m, snapshot.Stats.AiSpendThisMonthUsd);
        Assert.True(snapshot.Stats.AiPricingComplete);
        Assert.True(snapshot.Requests.Sum(point => point.Requests) >= 2);
        Assert.True(snapshot.Requests.Sum(point => point.Errors) >= 1);
        Assert.Contains(snapshot.LogLevels, level => level.Level == "Error" && level.Count >= 1);
        Assert.Contains(snapshot.AiUsage, point => point.InputTokens == 100 && point.OutputTokens == 25);
        Assert.Contains(snapshot.RecentSecurityEvents, entry => entry.Event == "LoginFailed");
        Assert.Contains(snapshot.TopErrors, error => error.ExceptionType == "System.InvalidOperationException");
    }

    [Fact]
    public async Task GetLogs_FiltersSecurityByModuleSource()
    {
        var client = await CreateAuthorizedClientAsync();
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(-1).ToString("O"));

        var response = await client.GetAsync($"/admin/logs?category=security&source=Auth&from={from}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<LogPageDto>();
        Assert.NotNull(page);
        var entry = Assert.Single(page.Entries);
        Assert.Equal("LoginFailed", entry.Properties["SecurityEvent"].GetString());
    }

    [Fact]
    public async Task GetActivity_FiltersByTypeAndTextAndPaginatesNewestFirst()
    {
        var client = await CreateAuthorizedClientAsync();
        await SeedActivityAsync();

        var firstResponse = await client.GetAsync("/admin/activity?type=processingcompleted&search=report&page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<ActivityPageDto>();
        Assert.NotNull(firstPage);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(1, firstPage.PageSize);
        Assert.True(firstPage.HasMore);
        var firstEntry = Assert.Single(firstPage.Entries);
        Assert.Equal(ActivityTypes.ProcessingCompleted, firstEntry.Type);
        Assert.Equal("Finished processing report-v2.pdf", firstEntry.Summary);

        var secondResponse = await client.GetAsync("/admin/activity?type=ProcessingCompleted&search=REPORT&page=2&pageSize=1");
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<ActivityPageDto>();
        Assert.NotNull(secondPage);
        Assert.False(secondPage.HasMore);
        Assert.Equal("Finished processing report-v1.pdf", Assert.Single(secondPage.Entries).Summary);
    }

    private async Task SeedActivityAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        var start = new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero);
        await dbContext.ActivityEntries.AddRangeAsync([
            CreateActivity(ActivityTypes.FileUploaded, "Uploaded report.pdf", start),
            CreateActivity(ActivityTypes.ProcessingCompleted, "Finished processing report-v1.pdf", start.AddMinutes(1)),
            CreateActivity(ActivityTypes.ProcessingCompleted, "Finished processing report-v2.pdf", start.AddMinutes(2)),
            CreateActivity(ActivityTypes.ProcessingFailed, "Failed processing notes.txt", start.AddMinutes(3))
        ]);
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedAiUsageAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        await dbContext.AiUsageRecords.AddAsync(new AiUsageRecord
        {
            Id = Guid.NewGuid(),
            Provider = "huggingface",
            Model = "test-model",
            Operation = "rag.answer",
            InputTokens = 100,
            OutputTokens = 25,
            LatencyMilliseconds = 420,
            Succeeded = true,
            EstimatedCostUsd = 0.00123m,
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-2)
        });
        await dbContext.SaveChangesAsync();
    }

    private static string Clef(
        DateTimeOffset timestamp,
        string message,
        string level,
        Dictionary<string, object?> properties)
    {
        properties["@t"] = timestamp;
        properties["@m"] = message;
        properties["@l"] = level;
        return JsonSerializer.Serialize(properties);
    }

    private static ActivityEntry CreateActivity(string type, string summary, DateTimeOffset occurredAt) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Summary = summary,
        OccurredAt = occurredAt
    };

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

    private sealed record ActivityPageDto(int Page, int PageSize, bool HasMore, IReadOnlyList<ActivityEntryDto> Entries);

    private sealed record ActivityEntryDto(
        Guid Id,
        string Type,
        string Summary,
        DateTimeOffset OccurredAt,
        Guid? FileId,
        Guid? ConversationId);

    private sealed record ObservabilitySnapshotDto(
        string Range,
        ObservabilityStatsDto Stats,
        IReadOnlyList<RequestMetricPointDto> Requests,
        IReadOnlyList<LogLevelCountDto> LogLevels,
        IReadOnlyList<AiUsageMetricPointDto> AiUsage,
        IReadOnlyList<SecurityEventSummaryDto> RecentSecurityEvents,
        IReadOnlyList<TopErrorSummaryDto> TopErrors);

    private sealed record ObservabilityStatsDto(
        int RequestsToday,
        double ErrorRatePercent,
        double AverageLatencyMilliseconds,
        decimal AiSpendThisMonthUsd,
        bool AiPricingComplete);

    private sealed record RequestMetricPointDto(DateTimeOffset Timestamp, int Requests, int Errors, double ErrorRatePercent);

    private sealed record LogLevelCountDto(string Level, int Count);

    private sealed record AiUsageMetricPointDto(DateTimeOffset Timestamp, int InputTokens, int OutputTokens);

    private sealed record SecurityEventSummaryDto(string Event);

    private sealed record TopErrorSummaryDto(string? ExceptionType);
}
