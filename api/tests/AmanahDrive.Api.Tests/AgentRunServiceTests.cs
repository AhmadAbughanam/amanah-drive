using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AmanahDrive.Api.Modules.Agent.Models;
using AmanahDrive.Api.Modules.Agent.Services;
using AmanahDrive.Api.Shared.Infrastructure.Ai;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace AmanahDrive.Api.Tests;

public sealed class AgentRunServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("amanah_drive_agent_run_tests").WithUsername("postgres").WithPassword("postgres").Build();
    private readonly FakeAiProcessingClient _aiClient = new();
    private AmanahDriveApiFactory _factory = null!;
    private Guid _userId;
    private string _accessToken = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new AmanahDriveApiFactory(_postgres.GetConnectionString(), configureServices: services =>
        {
            services.RemoveAll<IAiProcessingClient>();
            services.AddSingleton<IAiProcessingClient>(_aiClient);
        });
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register")
        {
            Content = JsonContent.Create(new { Email = TestUsers.Email, Password = TestUsers.Password })
        };
        request.Headers.Add("X-Bootstrap-Token", TestUsers.BootstrapToken);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _accessToken = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!.AccessToken;
        using var scope = _factory.Services.CreateScope();
        _userId = await scope.ServiceProvider.GetRequiredService<AmanahDriveDbContext>().AdminUsers
            .Where(user => user.Email == TestUsers.Email).Select(user => user.Id).SingleAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task AutoApprovedTool_ExecutesAndContinuesWithToolRoleMessage()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAgentRunService>();
        _aiClient.Enqueue(ToolCall("create_folder", "{\"name\":\"Agent notes\",\"parentFolderId\":null}"));
        _aiClient.Enqueue(Final("Created the folder."));

        var run = await service.StartAsync(_userId, "Create a notes folder", null, CancellationToken.None);

        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Equal("Created the folder.", run.FinalAnswer);
        Assert.Equal(2, _aiClient.AgentRequests.Count);
        Assert.Contains(_aiClient.AgentRequests[1].Messages, message => message.Role == "tool");
    }

    [Fact]
    public async Task ApprovalGate_PausesThenApproveResumes()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAgentRunService>();
        _aiClient.Enqueue(ToolCall("rename_folder", "{\"folderId\":\"00000000-0000-0000-0000-000000000001\",\"name\":\"Archive\"}"));
        _aiClient.Enqueue(Final("The rename was attempted."));

        var pending = await service.StartAsync(_userId, "Rename a folder", null, CancellationToken.None);
        Assert.Equal(AgentRunStatus.AwaitingApproval, pending.Status);

        var completed = await service.ApproveAsync(_userId, pending.Id, CancellationToken.None);
        Assert.NotNull(completed);
        Assert.Equal(AgentRunStatus.Completed, completed.Status);
        Assert.Contains(_aiClient.AgentRequests[1].Messages, message => message.Role == "tool");
    }

    [Fact]
    public async Task Rejection_FeedsToolResultBackAndResumes()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAgentRunService>();
        _aiClient.Enqueue(ToolCall("move_file", "{\"fileId\":\"00000000-0000-0000-0000-000000000001\",\"destinationFolderId\":null}"));
        _aiClient.Enqueue(Final("I will not move the file."));

        var pending = await service.StartAsync(_userId, "Move a file", null, CancellationToken.None);
        var completed = await service.RejectAsync(_userId, pending.Id, CancellationToken.None);

        Assert.NotNull(completed);
        Assert.Equal(AgentRunStatus.Completed, completed.Status);
        var toolMessage = Assert.Single(_aiClient.AgentRequests[1].Messages, message => message.Role == "tool");
        Assert.Contains("rejected", toolMessage.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IterationCap_StopsRunBeforeNinthModelCall()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAgentRunService>();
        for (var index = 0; index < 8; index++)
        {
            _aiClient.Enqueue(ToolCall("create_folder", $"{{\"name\":\"Folder {index}\",\"parentFolderId\":null}}"));
        }
        var run = await service.StartAsync(_userId, "Keep creating folders", null, CancellationToken.None);

        Assert.Equal(AgentRunStatus.IterationLimitReached, run.Status);
        Assert.Equal(8, _aiClient.AgentRequests.Count);
    }

    [Fact]
    public async Task FollowUp_IncludesPriorRunStepsInMessageHistory()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAgentRunService>();
        _aiClient.Enqueue(Final("I found the quarterly report."));
        _aiClient.Enqueue(Final("I will call it Q3 instead."));

        var first = await service.StartAsync(_userId, "Find the quarterly report", null, CancellationToken.None);
        var followUp = await service.StartAsync(_userId, "Actually call it Q3 instead", first.ConversationId, CancellationToken.None);

        Assert.Equal(first.ConversationId, followUp.ConversationId);
        var messages = _aiClient.AgentRequests[1].Messages.ToList();
        Assert.Equal(["system", "user", "assistant", "user"], messages.Select(message => message.Role));
        Assert.Equal("Find the quarterly report", messages[1].Content);
        Assert.Equal("I found the quarterly report.", messages[2].Content);
        Assert.Equal("Actually call it Q3 instead", messages[3].Content);
    }

    [Fact]
    public async Task FollowUp_StartsWithFreshIterationBudget()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAgentRunService>();
        for (var index = 0; index < 8; index++)
        {
            _aiClient.Enqueue(ToolCall("create_folder", $"{{\"name\":\"First run {index}\",\"parentFolderId\":null}}"));
        }
        _aiClient.Enqueue(Final("The follow-up completed."));

        var exhausted = await service.StartAsync(_userId, "Use the whole budget", null, CancellationToken.None);
        var followUp = await service.StartAsync(_userId, "Now answer this", exhausted.ConversationId, CancellationToken.None);

        Assert.Equal(AgentRunStatus.IterationLimitReached, exhausted.Status);
        Assert.Equal(AgentRunStatus.Completed, followUp.Status);
        Assert.Equal("The follow-up completed.", followUp.FinalAnswer);
        Assert.Equal(9, _aiClient.AgentRequests.Count);
        Assert.Single(followUp.Steps, step => step.Role == "assistant");
    }

    [Fact]
    public async Task FollowUp_IsRejectedWhileLatestRunAwaitsApproval()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAgentRunService>();
        _aiClient.Enqueue(ToolCall("rename_folder", "{\"folderId\":\"00000000-0000-0000-0000-000000000001\",\"name\":\"Archive\"}"));

        var pending = await service.StartAsync(_userId, "Rename a folder", null, CancellationToken.None);

        await Assert.ThrowsAsync<AgentConversationNotReadyException>(() =>
            service.StartAsync(_userId, "Use a different name", pending.ConversationId, CancellationToken.None));
        Assert.Equal(AgentRunStatus.AwaitingApproval, pending.Status);
        Assert.Single(_aiClient.AgentRequests);
    }

    [Fact]
    public async Task FollowUp_RequiresConversationOwnership()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAgentRunService>();
        _aiClient.Enqueue(Final("Owner response."));
        var owned = await service.StartAsync(_userId, "Start a conversation", null, CancellationToken.None);

        await Assert.ThrowsAsync<AgentConversationNotFoundException>(() =>
            service.StartAsync(Guid.NewGuid(), "Try to continue it", owned.ConversationId, CancellationToken.None));
        Assert.Single(_aiClient.AgentRequests);
    }

    [Fact]
    public async Task FollowUpEndpoint_ReturnsAccumulatedConversationTranscript()
    {
        var client = CreateAuthorizedClient();
        _aiClient.Enqueue(Final("First answer."));
        _aiClient.Enqueue(Final("Follow-up answer."));

        using var firstResponse = await client.PostAsJsonAsync("/agent/runs", new { Question = "First instruction" });
        var first = (await firstResponse.Content.ReadFromJsonAsync<AgentRunResponseDto>())!;
        using var followUpResponse = await client.PostAsJsonAsync("/agent/runs", new { Question = "Follow-up instruction", first.ConversationId });
        var followUp = (await followUpResponse.Content.ReadFromJsonAsync<AgentRunResponseDto>())!;

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, followUpResponse.StatusCode);
        Assert.Equal(first.ConversationId, followUp.ConversationId);
        Assert.Equal(["First instruction", "First answer.", "Follow-up instruction", "Follow-up answer."], followUp.Steps.Select(step => step.Content));
        Assert.Equal(2, followUp.Steps.Select(step => step.RunId).Distinct().Count());
    }

    [Fact]
    public async Task FollowUpEndpoint_ReturnsConflictWhileLatestRunAwaitsApproval()
    {
        var client = CreateAuthorizedClient();
        _aiClient.Enqueue(ToolCall("rename_folder", "{\"folderId\":\"00000000-0000-0000-0000-000000000001\",\"name\":\"Archive\"}"));

        using var firstResponse = await client.PostAsJsonAsync("/agent/runs", new { Question = "Rename a folder" });
        var pending = (await firstResponse.Content.ReadFromJsonAsync<AgentRunResponseDto>())!;
        using var followUpResponse = await client.PostAsJsonAsync("/agent/runs", new { Question = "Use another name", pending.ConversationId });

        Assert.Equal(HttpStatusCode.Conflict, followUpResponse.StatusCode);
    }

    [Fact]
    public async Task FollowUpEndpoint_ReturnsNotFoundForUnknownConversation()
    {
        var client = CreateAuthorizedClient();

        using var response = await client.PostAsJsonAsync("/agent/runs", new { Question = "Continue", ConversationId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_aiClient.AgentRequests);
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return client;
    }

    private static AgentCompletionResponse Final(string content) =>
        new(new AgentChatMessage("assistant", content), "fake", new AiModelUsage("fake", 1, 1));

    private static AgentCompletionResponse ToolCall(string name, string arguments) =>
        new(new AgentChatMessage("assistant", null, ToolCalls: [new AgentToolCall("call-1", "function", new AgentToolCallFunction(name, arguments))]), "fake", new AiModelUsage("fake", 1, 1));

    private sealed record AuthResponseDto(string AccessToken);

    private sealed record AgentRunResponseDto(Guid Id, Guid ConversationId, string Status, IReadOnlyList<AgentRunStepResponseDto> Steps);

    private sealed record AgentRunStepResponseDto(Guid RunId, int Sequence, string Role, string? Content);

    private sealed class FakeAiProcessingClient : IAiProcessingClient
    {
        private readonly Queue<AgentCompletionResponse> _responses = [];
        public List<AgentCompletionRequest> AgentRequests { get; } = [];
        public void Enqueue(AgentCompletionResponse response) => _responses.Enqueue(response);
        public Task<ExtractResponse> ExtractAsync(string fileName, string contentType, Stream fileStream, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ChunkResponse> ChunkAsync(string text, int chunkSize, int overlap, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<EmbedResponse> EmbedAsync(IReadOnlyCollection<string> texts, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RagAnswerResponse> AnswerAsync(RagAnswerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AgentCompletionResponse> CompleteAgentAsync(AgentCompletionRequest request, CancellationToken cancellationToken)
        {
            AgentRequests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
