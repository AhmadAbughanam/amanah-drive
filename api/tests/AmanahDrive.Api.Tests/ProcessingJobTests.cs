using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AmanahDrive.Api.Ai;
using AmanahDrive.Api.Data;
using AmanahDrive.Api.Models;
using AmanahDrive.Api.Processing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace AmanahDrive.Api.Tests;

public sealed class ProcessingJobTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("amanah_drive_processing_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "amanah-drive-processing-tests", Guid.NewGuid().ToString("N"));

    private AmanahDriveApiFactory _factory = null!;
    private FakeAiProcessingClient _aiClient = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_storageRoot);
        await _postgres.StartAsync();

        _aiClient = new FakeAiProcessingClient();
        _factory = new AmanahDriveApiFactory(
            _postgres.GetConnectionString(),
            _storageRoot,
            maxFileSizeBytes: 1024,
            allowedContentTypes: ["text/plain"],
            configureServices: services =>
            {
                services.RemoveAll<IAiProcessingClient>();
                services.AddSingleton<IAiProcessingClient>(_aiClient);
            });

        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();

        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Upload_CreatesPendingProcessingJob()
    {
        var client = await CreateAuthorizedClientAsync();
        var file = await UploadTextFileAsync(client);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        var job = await dbContext.ProcessingJobs.SingleAsync(job => job.FileItemId == file.Id);

        Assert.Equal(ProcessingJobStatus.Pending, job.Status);
        Assert.Equal(file.ProcessingJobId, job.Id);
    }

    [Fact]
    public async Task Worker_WhenAiServiceSucceeds_StoresChunksAndCompletesJob()
    {
        var client = await CreateAuthorizedClientAsync();
        var file = await UploadTextFileAsync(client);

        using var scope = _factory.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<ProcessingJobRunner>();
        var processed = await runner.ProcessNextPendingJobAsync(CancellationToken.None);

        Assert.True(processed);

        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        var job = await dbContext.ProcessingJobs.SingleAsync(job => job.FileItemId == file.Id);
        var chunks = await dbContext.DocumentChunks
            .Where(chunk => chunk.FileItemId == file.Id)
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToListAsync();

        Assert.Equal(ProcessingJobStatus.Completed, job.Status);
        Assert.Null(job.ErrorMessage);
        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, chunk => Assert.Equal(384, chunk.Embedding.Memory.Length));
    }

    [Fact]
    public async Task Worker_WhenAiServiceFails_MarksJobFailedAndContinues()
    {
        _aiClient.ShouldFailExtract = true;
        var client = await CreateAuthorizedClientAsync();
        var file = await UploadTextFileAsync(client);

        using var scope = _factory.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<ProcessingJobRunner>();
        var processed = await runner.ProcessNextPendingJobAsync(CancellationToken.None);

        Assert.True(processed);

        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        var job = await dbContext.ProcessingJobs.SingleAsync(job => job.FileItemId == file.Id);
        var chunkCount = await dbContext.DocumentChunks.CountAsync(chunk => chunk.FileItemId == file.Id);

        Assert.Equal(ProcessingJobStatus.Failed, job.Status);
        Assert.Contains("extract failed", job.ErrorMessage);
        Assert.Equal(0, chunkCount);
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await BootstrapAndGetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> BootstrapAndGetAccessTokenAsync(HttpClient client)
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

        var authResponse = await ReadJsonAsync<AuthResponseDto>(response);
        return authResponse.AccessToken;
    }

    private static async Task<FileDto> UploadTextFileAsync(HttpClient client)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("hello processing"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "processing.txt");

        var response = await client.PostAsync("/drive/files/upload", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync<FileDto>(response);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(value);
        return value;
    }

    private sealed record AuthResponseDto(string AccessToken);

    private sealed record FileDto(Guid Id, Guid? ProcessingJobId);

    private sealed class FakeAiProcessingClient : IAiProcessingClient
    {
        public bool ShouldFailExtract { get; set; }

        public Task<ExtractResponse> ExtractAsync(string fileName, string contentType, Stream fileStream, CancellationToken cancellationToken)
        {
            if (ShouldFailExtract)
            {
                throw new AiServiceException("extract failed");
            }

            return Task.FromResult(new ExtractResponse("alpha beta gamma", contentType, 16));
        }

        public Task<ChunkResponse> ChunkAsync(string text, int chunkSize, int overlap, CancellationToken cancellationToken) =>
            Task.FromResult(new ChunkResponse([
                new ChunkDto(0, "alpha beta", 0, 10),
                new ChunkDto(1, "gamma", 11, 16)
            ]));

        public Task<EmbedResponse> EmbedAsync(IReadOnlyCollection<string> texts, CancellationToken cancellationToken) =>
            Task.FromResult(new EmbedResponse("fake", 384, texts.Select((_, index) => Enumerable.Repeat((float)index, 384).ToArray()).ToList()));

        public Task<RagAnswerResponse> AnswerAsync(RagAnswerRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new RagAnswerResponse("unused", "fake", []));
    }
}
