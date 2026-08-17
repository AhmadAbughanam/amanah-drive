using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AmanahDrive.Api.Modules.Admin.Activity;
using AmanahDrive.Api.Modules.Drive.Models;
using AmanahDrive.Api.Modules.Processing.Models;
using AmanahDrive.Api.Shared.Infrastructure.Ai;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector;
using Testcontainers.PostgreSql;

namespace AmanahDrive.Api.Tests;

public sealed class SearchChatEndpointTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("amanah_drive_search_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private AmanahDriveApiFactory _factory = null!;
    private FakeAiProcessingClient _aiClient = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _aiClient = new FakeAiProcessingClient();
        _factory = new AmanahDriveApiFactory(
            _postgres.GetConnectionString(),
            settings: new Dictionary<string, string?>
            {
                ["Search:ChatDefaultPageSize"] = "2",
                ["Search:ChatMaxPageSize"] = "3"
            },
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
    }

    [Fact]
    public async Task Search_ReturnsMostRelevantChunkFirst()
    {
        var client = await CreateAuthorizedClientAsync();
        await SeedChunksAsync();

        var response = await client.GetAsync("/search?query=renewal&topK=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<SearchResponseDto>(response);
        Assert.Equal(2, body.Results.Count);
        Assert.Equal("lease.pdf", body.Results[0].FileName);
        Assert.Contains("renews yearly", body.Results[0].Snippet);
        Assert.True(body.Results[0].Score >= body.Results[1].Score);
    }

    [Fact]
    public async Task Search_AfterRepeatedRequests_IsRateLimited()
    {
        var client = await CreateAuthorizedClientAsync();
        await SeedChunksAsync();

        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < 21; attempt++)
        {
            response = await client.GetAsync($"/search?query=renewal-{attempt}");
        }

        Assert.Equal((HttpStatusCode)429, response.StatusCode);
    }

    [Fact]
    public async Task Chat_ReturnsAnswerWithCitations()
    {
        var client = await CreateAuthorizedClientAsync();
        var chunks = await SeedChunksAsync();

        var response = await client.PostAsJsonAsync("/chat", new
        {
            Question = "What is the renewal rule?"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<ChatResponseDto>(response);
        Assert.NotEqual(Guid.Empty, body.ConversationId);
        Assert.Equal("Grounded answer from retrieved chunks.", body.Answer);
        Assert.Single(body.Citations);
        Assert.Equal(chunks.LeaseChunkId, body.Citations[0].ChunkId);
        Assert.Equal("lease.pdf", body.Citations[0].FileName);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        var activity = await dbContext.ActivityEntries.SingleAsync(entry => entry.ConversationId == body.ConversationId);
        Assert.Equal(ActivityTypes.ChatAnswered, activity.Type);
        Assert.Equal("Answered: What is the renewal rule?", activity.Summary);
    }

    [Fact]
    public async Task Chat_MapsNumericCitationReferenceToRetrievedChunk()
    {
        var client = await CreateAuthorizedClientAsync();
        var chunks = await SeedChunksAsync();
        _aiClient.CitationReference = "2";

        var response = await client.PostAsJsonAsync("/chat", new
        {
            Question = "What is the renewal rule?"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync<ChatResponseDto>(response);
        Assert.Single(body.Citations);
        Assert.Equal(chunks.PolicyChunkId, body.Citations[0].ChunkId);
        Assert.Equal("policy.md", body.Citations[0].FileName);
    }

    [Fact]
    public async Task Chat_AfterRepeatedRequests_IsRateLimited()
    {
        var client = await CreateAuthorizedClientAsync();
        await SeedChunksAsync();

        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < 21; attempt++)
        {
            response = await client.PostAsJsonAsync("/chat", new
            {
                Question = $"What is the renewal rule {attempt}?"
            });
        }

        Assert.Equal((HttpStatusCode)429, response.StatusCode);
    }

    [Fact]
    public async Task Chat_WithExistingConversation_IncludesHistoryAndPersistsMessages()
    {
        var client = await CreateAuthorizedClientAsync();
        await SeedChunksAsync();

        var firstResponse = await client.PostAsJsonAsync("/chat", new
        {
            Question = "What is the renewal rule?"
        });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var first = await ReadJsonAsync<ChatResponseDto>(firstResponse);

        var secondResponse = await client.PostAsJsonAsync("/chat", new
        {
            Question = "Does approval matter?",
            ConversationId = first.ConversationId
        });
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        Assert.Equal(2, _aiClient.AnswerRequests.Count);
        var secondRequest = _aiClient.AnswerRequests[1];
        Assert.Equal(2, secondRequest.History.Count);
        Assert.Equal("user", secondRequest.History.ElementAt(0).Role);
        Assert.Equal("What is the renewal rule?", secondRequest.History.ElementAt(0).Content);
        Assert.Equal("assistant", secondRequest.History.ElementAt(1).Role);
        Assert.Equal("Grounded answer from retrieved chunks.", secondRequest.History.ElementAt(1).Content);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        var messageCount = await dbContext.ChatMessages.CountAsync(message => message.ConversationId == first.ConversationId);
        Assert.Equal(4, messageCount);
    }

    [Fact]
    public async Task GetChatHistory_ReturnsMessagesInOrder()
    {
        var client = await CreateAuthorizedClientAsync();
        await SeedChunksAsync();

        var chatResponse = await client.PostAsJsonAsync("/chat", new
        {
            Question = "What is the renewal rule?"
        });
        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);
        var chat = await ReadJsonAsync<ChatResponseDto>(chatResponse);

        var historyResponse = await client.GetAsync($"/chat/{chat.ConversationId}");

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await ReadJsonAsync<ChatHistoryResponseDto>(historyResponse);
        Assert.Equal(chat.ConversationId, history.ConversationId);
        Assert.Equal(2, history.Messages.Count);
        Assert.Equal("user", history.Messages[0].Role);
        Assert.Equal("What is the renewal rule?", history.Messages[0].Content);
        Assert.Equal("assistant", history.Messages[1].Role);
        Assert.Equal("Grounded answer from retrieved chunks.", history.Messages[1].Content);
        Assert.Single(history.Messages[1].Citations);
    }

    [Fact]
    public async Task GetChatHistory_UsesPaginationDefaultsAndPageNavigation()
    {
        var client = await CreateAuthorizedClientAsync();
        await SeedChunksAsync();

        var firstChat = await PostChatAsync(client, "Question one?");
        await PostChatAsync(client, "Question two?", firstChat.ConversationId);
        await PostChatAsync(client, "Question three?", firstChat.ConversationId);

        var firstPageResponse = await client.GetAsync($"/chat/{firstChat.ConversationId}");
        Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
        var firstPage = await ReadJsonAsync<ChatHistoryResponseDto>(firstPageResponse);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.PageSize);
        Assert.Equal(["user", "assistant"], firstPage.Messages.Select(message => message.Role));
        Assert.Equal("Question one?", firstPage.Messages[0].Content);

        var secondPageResponse = await client.GetAsync($"/chat/{firstChat.ConversationId}?page=2&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
        var secondPage = await ReadJsonAsync<ChatHistoryResponseDto>(secondPageResponse);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(2, secondPage.PageSize);
        Assert.Equal("Question two?", secondPage.Messages[0].Content);
    }

    [Fact]
    public async Task GetChatHistory_ClampsPageSizeToConfiguredMaximum()
    {
        var client = await CreateAuthorizedClientAsync();
        await SeedChunksAsync();

        var firstChat = await PostChatAsync(client, "Question one?");
        await PostChatAsync(client, "Question two?", firstChat.ConversationId);

        var response = await client.GetAsync($"/chat/{firstChat.ConversationId}?pageSize=99");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = await ReadJsonAsync<ChatHistoryResponseDto>(response);
        Assert.Equal(3, history.PageSize);
        Assert.Equal(3, history.Messages.Count);
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await BootstrapAndGetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<SeededChunks> SeedChunksAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>();
        var user = await dbContext.AdminUsers.SingleAsync();
        var now = DateTimeOffset.UtcNow;

        var leaseFile = new FileItem
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OriginalFileName = "lease.pdf",
            StorageKey = Guid.NewGuid().ToString("N"),
            ContentType = "application/pdf",
            SizeBytes = 10,
            ChecksumSha256 = new string('a', 64),
            CreatedAt = now,
            UpdatedAt = now
        };
        var policyFile = new FileItem
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OriginalFileName = "policy.md",
            StorageKey = Guid.NewGuid().ToString("N"),
            ContentType = "text/markdown",
            SizeBytes = 10,
            ChecksumSha256 = new string('b', 64),
            CreatedAt = now,
            UpdatedAt = now
        };
        var leaseChunkId = Guid.NewGuid();
        var policyChunkId = Guid.NewGuid();

        await dbContext.FileItems.AddRangeAsync([leaseFile, policyFile]);
        await dbContext.DocumentChunks.AddRangeAsync([
            new DocumentChunk
            {
                Id = leaseChunkId,
                FileItemId = leaseFile.Id,
                ChunkIndex = 0,
                Text = "The lease renews yearly if notice is provided.",
                StartOffset = 0,
                EndOffset = 46,
                Embedding = new Vector(UnitVector(0)),
                CreatedAt = now
            },
            new DocumentChunk
            {
                Id = policyChunkId,
                FileItemId = policyFile.Id,
                ChunkIndex = 0,
                Text = "Approval is required before archival.",
                StartOffset = 0,
                EndOffset = 36,
                Embedding = new Vector(UnitVector(1)),
                CreatedAt = now
            }
        ]);
        await dbContext.SaveChangesAsync();

        return new SeededChunks(leaseChunkId, policyChunkId);
    }

    private static async Task<ChatResponseDto> PostChatAsync(HttpClient client, string question, Guid? conversationId = null)
    {
        var response = await client.PostAsJsonAsync("/chat", new
        {
            Question = question,
            ConversationId = conversationId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync<ChatResponseDto>(response);
    }

    private static float[] UnitVector(int axis)
    {
        var values = new float[384];
        values[axis] = 1;
        return values;
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

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(value);
        return value;
    }

    private sealed record SeededChunks(Guid LeaseChunkId, Guid PolicyChunkId);

    private sealed record AuthResponseDto(string AccessToken);

    private sealed record SearchResponseDto(IReadOnlyList<SearchResultDto> Results);

    private sealed record SearchResultDto(Guid ChunkId, Guid FileId, string FileName, int ChunkIndex, string Snippet, double Score);

    private sealed record ChatResponseDto(Guid ConversationId, string Answer, IReadOnlyList<ChatCitationDto> Citations);

    private sealed record ChatCitationDto(Guid ChunkId, Guid? FileId, string FileName, string Snippet);

    private sealed record ChatHistoryResponseDto(Guid ConversationId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Page, int PageSize, IReadOnlyList<ChatMessageDto> Messages);

    private sealed record ChatMessageDto(Guid Id, string Role, string Content, IReadOnlyList<ChatCitationDto> Citations, DateTimeOffset CreatedAt);

    private sealed class FakeAiProcessingClient : IAiProcessingClient
    {
        public List<RagAnswerRequest> AnswerRequests { get; } = [];

        public string CitationReference { get; set; } = "1";

        public Task<ExtractResponse> ExtractAsync(string fileName, string contentType, Stream fileStream, CancellationToken cancellationToken) =>
            Task.FromResult(new ExtractResponse("unused", contentType, 6));

        public Task<ChunkResponse> ChunkAsync(string text, int chunkSize, int overlap, CancellationToken cancellationToken) =>
            Task.FromResult(new ChunkResponse([]));

        public Task<EmbedResponse> EmbedAsync(IReadOnlyCollection<string> texts, CancellationToken cancellationToken)
        {
            var embeddings = texts
                .Select(text => text.Contains("approval", StringComparison.OrdinalIgnoreCase) ? UnitVector(1) : UnitVector(0))
                .ToList();

            return Task.FromResult(new EmbedResponse("fake", 384, embeddings));
        }

        public Task<RagAnswerResponse> AnswerAsync(RagAnswerRequest request, CancellationToken cancellationToken)
        {
            AnswerRequests.Add(request);
            var citationIndex = int.Parse(CitationReference) - 1;
            var citedChunk = request.Chunks.ElementAt(citationIndex);
            return Task.FromResult(new RagAnswerResponse(
                "Grounded answer from retrieved chunks.",
                "fake",
                [
                    new RagCitation(CitationReference, citedChunk.FileName, citedChunk.Text)
                ]));
        }
    }
}
