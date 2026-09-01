using System.Net;
using System.Net.Http.Json;
using AmanahDrive.Api.Modules.AgentTools;
using AmanahDrive.Api.Modules.AgentTools.Tools;
using AmanahDrive.Api.Modules.Auth.Models;
using AmanahDrive.Api.Modules.Drive.Models;
using AmanahDrive.Api.Modules.Drive.Storage;
using AmanahDrive.Api.Modules.Processing.Models;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;
using Testcontainers.PostgreSql;

namespace AmanahDrive.Api.Tests;

public sealed class AgentToolTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("amanah_drive_agent_tool_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "amanah-drive-agent-tool-tests", Guid.NewGuid().ToString("N"));
    private AmanahDriveApiFactory _factory = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_storageRoot);
        await _postgres.StartAsync();
        _factory = new AmanahDriveApiFactory(_postgres.GetConnectionString(), _storageRoot);
        await _factory.ResetDatabaseAsync();

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register")
        {
            Content = JsonContent.Create(new { Email = TestUsers.Email, Password = TestUsers.Password })
        };
        request.Headers.Add("X-Bootstrap-Token", TestUsers.BootstrapToken);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        _userId = await dbContext.AdminUsers.Where(user => user.Email == TestUsers.Email).Select(user => user.Id).SingleAsync();
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
    public async Task ReadFileText_WhenFileIsUnprocessed_ReturnsExplicitEmptyText()
    {
        var file = await SeedFileAsync("unprocessed.txt", "unprocessed"u8.ToArray());
        using var scope = _factory.Services.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<IAgentTool<ReadFileTextRequest, ReadFileTextResponse>>();

        var result = await tool.ExecuteAsync(new AgentToolContext(_userId), new ReadFileTextRequest(file.Id), CancellationToken.None);

        Assert.Equal(AgentToolStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.False(result.Value.HasExtractedText);
        Assert.False(result.Value.IsTruncated);
        Assert.Equal(string.Empty, result.Value.Text);
        Assert.Equal(0, result.Value.TotalCharacterCount);
    }

    [Fact]
    public async Task ReadFileText_WhenExtractedTextExceedsCap_ReturnsTruncationMetadata()
    {
        var file = await SeedFileAsync("large.txt", "large"u8.ToArray());
        await SeedChunkAsync(file.Id, 0, new string('x', 100_001));
        using var scope = _factory.Services.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<IAgentTool<ReadFileTextRequest, ReadFileTextResponse>>();

        var result = await tool.ExecuteAsync(new AgentToolContext(_userId), new ReadFileTextRequest(file.Id), CancellationToken.None);

        Assert.Equal(AgentToolStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.HasExtractedText);
        Assert.True(result.Value.IsTruncated);
        Assert.Equal(100_001, result.Value.TotalCharacterCount);
        Assert.Equal(100_000, result.Value.ReturnedCharacterCount);
        Assert.Equal(100_000, result.Value.Text.Length);
    }

    [Fact]
    public async Task ReadFileText_ConcatenatesChunksInChunkIndexOrder()
    {
        var file = await SeedFileAsync("ordered.txt", "ordered"u8.ToArray());
        await SeedChunkAsync(file.Id, 1, "second");
        await SeedChunkAsync(file.Id, 0, "first");
        using var scope = _factory.Services.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<IAgentTool<ReadFileTextRequest, ReadFileTextResponse>>();

        var result = await tool.ExecuteAsync(new AgentToolContext(_userId), new ReadFileTextRequest(file.Id), CancellationToken.None);

        Assert.Equal(AgentToolStatus.Success, result.Status);
        Assert.NotNull(result.Value);
        Assert.Equal("firstsecond", result.Value.Text);
    }

    [Fact]
    public async Task CreateFolderAndListFolder_UseTheDriveServiceOwnershipScope()
    {
        using var scope = _factory.Services.CreateScope();
        var createTool = scope.ServiceProvider.GetRequiredService<IAgentTool<CreateFolderToolRequest, CreateFolderToolResponse>>();
        var listTool = scope.ServiceProvider.GetRequiredService<IAgentTool<ListFolderRequest, ListFolderResponse>>();
        var context = new AgentToolContext(_userId);

        var created = await createTool.ExecuteAsync(context, new CreateFolderToolRequest("Agent folder", null), CancellationToken.None);
        var listed = await listTool.ExecuteAsync(context, new ListFolderRequest(null), CancellationToken.None);

        Assert.Equal(AgentToolStatus.Success, created.Status);
        Assert.Equal(AgentToolStatus.Success, listed.Status);
        Assert.NotNull(created.Value);
        Assert.NotNull(listed.Value);
        Assert.Contains(listed.Value.Contents.Folders, folder => folder.Id == created.Value.Folder.Id);
    }

    [Fact]
    public async Task CopyFile_CopiesBytesAndRejectsDestinationNameCollisions()
    {
        var sourceFile = await SeedFileAsync("source.txt", "same bytes"u8.ToArray());
        using var scope = _factory.Services.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<IAgentTool<CopyFileToolRequest, CopyFileToolResponse>>();
        var context = new AgentToolContext(_userId);

        var copied = await tool.ExecuteAsync(context, new CopyFileToolRequest(sourceFile.Id, null, "copy.txt"), CancellationToken.None);
        var collision = await tool.ExecuteAsync(context, new CopyFileToolRequest(sourceFile.Id, null, "copy.txt"), CancellationToken.None);

        Assert.Equal(AgentToolStatus.Success, copied.Status);
        Assert.NotNull(copied.Value);
        Assert.Equal(AgentToolStatus.Conflict, collision.Status);
        Assert.Equal("A file with that name already exists in this location.", collision.ErrorMessage);

        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        var copiedFile = await dbContext.FileItems.SingleAsync(file => file.Id == copied.Value.File.Id);
        Assert.NotEqual(sourceFile.StorageKey, copiedFile.StorageKey);
        Assert.Equal(ProcessingJobStatus.Pending, await dbContext.ProcessingJobs.Where(job => job.FileItemId == copiedFile.Id).Select(job => job.Status).SingleAsync());

        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        await using var copiedContent = await storage.OpenReadAsync(copiedFile.StorageKey!, CancellationToken.None);
        using var buffer = new MemoryStream();
        await copiedContent.CopyToAsync(buffer);
        Assert.Equal("same bytes"u8.ToArray(), buffer.ToArray());
    }

    [Fact]
    public async Task CopyFile_YouTubeItem_ReturnsClearInvalidResultWithoutOpeningStorage()
    {
        var sourceFile = await SeedYouTubeFileAsync();
        using var scope = _factory.Services.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService<IAgentTool<CopyFileToolRequest, CopyFileToolResponse>>();

        var result = await tool.ExecuteAsync(new AgentToolContext(_userId), new CopyFileToolRequest(sourceFile.Id, null, "copy.txt"), CancellationToken.None);

        Assert.Equal(AgentToolStatus.Invalid, result.Status);
        Assert.Equal("YouTube items cannot be copied because no source bytes are stored.", result.ErrorMessage);
    }

    [Fact]
    public void ToolApprovalFlags_AreDeclaredOnTheToolImplementations()
    {
        using var scope = _factory.Services.CreateScope();

        Assert.False(scope.ServiceProvider.GetRequiredService<IAgentTool<ListFolderRequest, ListFolderResponse>>().RequiresApproval);
        Assert.False(scope.ServiceProvider.GetRequiredService<IAgentTool<SearchFilesRequest, SearchFilesResponse>>().RequiresApproval);
        Assert.False(scope.ServiceProvider.GetRequiredService<IAgentTool<ReadFileTextRequest, ReadFileTextResponse>>().RequiresApproval);
        Assert.False(scope.ServiceProvider.GetRequiredService<IAgentTool<ListGitHubDirectoryRequest, GitHubDirectoryResponse>>().RequiresApproval);
        Assert.False(scope.ServiceProvider.GetRequiredService<IAgentTool<ReadGitHubFileRequest, GitHubFileTextResponse>>().RequiresApproval);
        Assert.False(scope.ServiceProvider.GetRequiredService<IAgentTool<CreateFolderToolRequest, CreateFolderToolResponse>>().RequiresApproval);
        Assert.False(scope.ServiceProvider.GetRequiredService<IAgentTool<CopyFileToolRequest, CopyFileToolResponse>>().RequiresApproval);
        Assert.True(scope.ServiceProvider.GetRequiredService<IAgentTool<RenameFolderToolRequest, RenameFolderToolResponse>>().RequiresApproval);
        Assert.True(scope.ServiceProvider.GetRequiredService<IAgentTool<RenameFileToolRequest, RenameFileToolResponse>>().RequiresApproval);
        Assert.True(scope.ServiceProvider.GetRequiredService<IAgentTool<MoveFileToolRequest, MoveFileToolResponse>>().RequiresApproval);
    }

    private async Task<FileItem> SeedFileAsync(string fileName, byte[] content)
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        await using var stream = new MemoryStream(content);
        var stored = await storage.SaveAsync(stream, CancellationToken.None);
        var file = new FileItem
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            OriginalFileName = fileName,
            StorageKey = stored.StorageKey,
            ContentType = "text/plain",
            SizeBytes = stored.SizeBytes,
            ChecksumSha256 = stored.ChecksumSha256,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        await dbContext.FileItems.AddAsync(file);
        await dbContext.SaveChangesAsync();
        return file;
    }

    private async Task SeedChunkAsync(Guid fileId, int chunkIndex, string text)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        await dbContext.DocumentChunks.AddAsync(new DocumentChunk
        {
            Id = Guid.NewGuid(),
            FileItemId = fileId,
            ChunkIndex = chunkIndex,
            Text = text,
            StartOffset = 0,
            EndOffset = text.Length,
            Embedding = new Vector(new float[384]),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<FileItem> SeedYouTubeFileAsync()
    {
        var file = new FileItem
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            OriginalFileName = "Video.youtube.txt",
            Source = FileSource.YouTube,
            SourceUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            ContentType = "text/plain",
            SizeBytes = 0,
            ChecksumSha256 = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        await dbContext.FileItems.AddAsync(file);
        await dbContext.SaveChangesAsync();
        return file;
    }
}
