using System.Net;
using System.Net.Http.Json;
using AmanahDrive.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace AmanahDrive.Api.Tests;

public sealed class AuthEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("amanah_drive_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private AmanahDriveApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new AmanahDriveApiFactory(_postgres.GetConnectionString());
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAccessTokenAndRefreshCookie()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        await BootstrapAdminAsync(client);

        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            Email = TestUsers.Email,
            Password = TestUsers.Password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Set-Cookie", response.Headers.Select(header => header.Key));

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.False(string.IsNullOrWhiteSpace(authResponse?.AccessToken));
    }

    [Fact]
    public async Task Register_AfterRepeatedInvalidBootstrapAttempts_IsRateLimited()
    {
        var client = _factory.CreateClient();

        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < 21; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register")
            {
                Content = JsonContent.Create(new
                {
                    Email = $"admin-{attempt}@example.com",
                    Password = TestUsers.Password
                })
            };
            request.Headers.Add("X-Bootstrap-Token", "wrong-bootstrap-token");

            response = await client.SendAsync(request);
        }

        Assert.Equal((HttpStatusCode)429, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        await BootstrapAdminAsync(client);

        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            Email = TestUsers.Email,
            Password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_AfterRepeatedFailures_LocksAccount()
    {
        var client = _factory.CreateClient();
        await BootstrapAdminAsync(client);

        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            response = await client.PostAsJsonAsync("/auth/login", new
            {
                Email = TestUsers.Email,
                Password = "wrong-password"
            });
        }

        Assert.Equal((HttpStatusCode)423, response.StatusCode);

        var lockedResponse = await client.PostAsJsonAsync("/auth/login", new
        {
            Email = TestUsers.Email,
            Password = TestUsers.Password
        });

        Assert.Equal((HttpStatusCode)423, lockedResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesRefreshToken()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var registerResponse = await BootstrapAdminAsync(client);
        var originalCookie = GetRefreshCookie(registerResponse);

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        refreshRequest.Headers.Add("Cookie", originalCookie);

        var refreshResponse = await client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotatedCookie = GetRefreshCookie(refreshResponse);
        Assert.NotEqual(originalCookie, rotatedCookie);
    }

    [Fact]
    public async Task Refresh_WithReusedToken_IsRejected()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var registerResponse = await BootstrapAdminAsync(client);
        var originalCookie = GetRefreshCookie(registerResponse);

        using var firstRefresh = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        firstRefresh.Headers.Add("Cookie", originalCookie);
        var firstRefreshResponse = await client.SendAsync(firstRefresh);
        Assert.Equal(HttpStatusCode.OK, firstRefreshResponse.StatusCode);

        using var reuseRefresh = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        reuseRefresh.Headers.Add("Cookie", originalCookie);
        var reuseResponse = await client.SendAsync(reuseRefresh);

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_IsRejected()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        await BootstrapAdminAsync(client);

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        refreshRequest.Headers.Add("Cookie", "__Host-amanah-refresh=invalid-token");

        var response = await client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> BootstrapAdminAsync(HttpClient client)
    {
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
        return response;
    }

    private static string GetRefreshCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(header => header.StartsWith("__Host-amanah-refresh=", StringComparison.Ordinal));

        return setCookie.Split(';', 2)[0];
    }

    private sealed record AuthResponseDto(string AccessToken);
}

internal sealed class AmanahDriveApiFactory(
    string connectionString,
    string? storageRoot = null,
    long? maxFileSizeBytes = null,
    string[]? allowedContentTypes = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Auth:JwtIssuer", "AmanahDrive.Tests");
        builder.UseSetting("Auth:JwtAudience", "AmanahDrive.Tests");
        builder.UseSetting("Auth:JwtSigningKey", "tests-only-signing-key-with-at-least-32-chars");
        builder.UseSetting("Auth:BootstrapToken", TestUsers.BootstrapToken);
        builder.UseSetting("Auth:SecureCookies", "false");

        if (storageRoot is not null)
        {
            builder.UseSetting("Drive:StorageRoot", storageRoot);
        }

        if (maxFileSizeBytes is not null)
        {
            builder.UseSetting("Drive:MaxFileSizeBytes", maxFileSizeBytes.Value.ToString());
        }

        if (allowedContentTypes is not null)
        {
            for (var index = 0; index < allowedContentTypes.Length; index++)
            {
                builder.UseSetting($"Drive:AllowedContentTypes:{index}", allowedContentTypes[index]);
            }
        }
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}

internal static class TestUsers
{
    public const string Email = "admin@example.com";
    public const string Password = "correct horse battery staple";
    public const string BootstrapToken = "tests-only-bootstrap-token";
}
