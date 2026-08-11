using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace AmanahDrive.Api.Tests;

public sealed class DriveEndpointTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("amanah_drive_drive_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "amanah-drive-tests", Guid.NewGuid().ToString("N"));

    private AmanahDriveApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_storageRoot);
        await _postgres.StartAsync();
        _factory = new AmanahDriveApiFactory(
            _postgres.GetConnectionString(),
            _storageRoot,
            maxFileSizeBytes: 64,
            allowedContentTypes: ["text/plain"]);
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
    public async Task FolderCrud_CreateListRenameDelete_Works()
    {
        var client = await CreateAuthorizedClientAsync();

        var created = await CreateFolderAsync(client, "Documents");

        var rootContents = await GetFolderContentsAsync(client);
        Assert.Contains(rootContents.Folders, folder => folder.Id == created.Id && folder.Name == "Documents");

        var renameResponse = await client.PatchAsJsonAsync($"/drive/folders/{created.Id}/rename", new { Name = "Archive" });
        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);

        var renamedContents = await GetFolderContentsAsync(client);
        Assert.Contains(renamedContents.Folders, folder => folder.Id == created.Id && folder.Name == "Archive");

        var deleteResponse = await client.DeleteAsync($"/drive/folders/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var deletedContents = await GetFolderContentsAsync(client);
        Assert.DoesNotContain(deletedContents.Folders, folder => folder.Id == created.Id);
    }

    [Fact]
    public async Task UploadDownload_ReturnsOriginalBytes()
    {
        var client = await CreateAuthorizedClientAsync();
        var folder = await CreateFolderAsync(client, "Documents");
        var expectedBytes = "hello secure drive"u8.ToArray();

        var file = await UploadFileAsync(client, expectedBytes, "note.txt", "text/plain", folder.Id);

        var downloadResponse = await client.GetAsync($"/drive/files/{file.Id}/download");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);

        var actualBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(expectedBytes, actualBytes);
    }

    [Fact]
    public async Task Upload_WhenOversized_IsRejected()
    {
        var client = await CreateAuthorizedClientAsync();
        var oversizedBytes = Enumerable.Repeat((byte)'a', 65).ToArray();

        using var content = CreateUploadContent(oversizedBytes, "too-large.txt", "text/plain");
        var response = await client.PostAsync("/drive/files/upload", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WhenMimeTypeDisallowed_IsRejected()
    {
        var client = await CreateAuthorizedClientAsync();

        using var content = CreateUploadContent("hello"u8.ToArray(), "payload.bin", "application/octet-stream");
        var response = await client.PostAsync("/drive/files/upload", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WhenFilenameContainsPathTraversal_IsRejected()
    {
        var client = await CreateAuthorizedClientAsync();

        using var content = CreateUploadContent("hello"u8.ToArray(), "../evil.txt", "text/plain");
        var response = await client.PostAsync("/drive/files/upload", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFolder_RemovesStoredFiles()
    {
        var client = await CreateAuthorizedClientAsync();
        var folder = await CreateFolderAsync(client, "Documents");
        await UploadFileAsync(client, "hello"u8.ToArray(), "note.txt", "text/plain", folder.Id);

        Assert.NotEmpty(Directory.EnumerateFiles(_storageRoot, "*", SearchOption.AllDirectories));

        var response = await client.DeleteAsync($"/drive/folders/{folder.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(Directory.EnumerateFiles(_storageRoot, "*", SearchOption.AllDirectories));
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

    private static async Task<FolderDto> CreateFolderAsync(HttpClient client, string name, Guid? parentFolderId = null)
    {
        var response = await client.PostAsJsonAsync("/drive/folders", new
        {
            Name = name,
            ParentFolderId = parentFolderId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync<FolderDto>(response);
    }

    private static async Task<FolderContentsDto> GetFolderContentsAsync(HttpClient client, Guid? parentFolderId = null)
    {
        var url = parentFolderId is null ? "/drive/folders" : $"/drive/folders?parentFolderId={parentFolderId}";
        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync<FolderContentsDto>(response);
    }

    private static async Task<FileDto> UploadFileAsync(HttpClient client, byte[] bytes, string fileName, string contentType, Guid? folderId = null)
    {
        using var content = CreateUploadContent(bytes, fileName, contentType, folderId);
        var response = await client.PostAsync("/drive/files/upload", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync<FileDto>(response);
    }

    private static MultipartFormDataContent CreateUploadContent(byte[] bytes, string fileName, string contentType, Guid? folderId = null)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        if (folderId is not null)
        {
            form.Add(new StringContent(folderId.Value.ToString(), Encoding.UTF8), "folderId");
        }

        return form;
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(value);
        return value;
    }

    private sealed record AuthResponseDto(string AccessToken);

    private sealed record FolderDto(Guid Id, string Name, Guid? ParentFolderId);

    private sealed record FileDto(Guid Id, Guid? FolderId, string OriginalFileName, string ContentType, long SizeBytes, string ChecksumSha256);

    private sealed record FolderContentsDto(Guid? ParentFolderId, IReadOnlyCollection<FolderDto> Folders, IReadOnlyCollection<FileDto> Files);
}
