using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using AmanahDrive.Api.Ai;
using AmanahDrive.Api.Data;
using AmanahDrive.Api.Models;
using AmanahDrive.Api.Options;
using AmanahDrive.Api.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Endpoints;

public static class SearchChatEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapSearchChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/search", SearchAsync).RequireAuthorization().RequireRateLimiting("ai");
        app.MapPost("/chat", ChatAsync).RequireAuthorization().RequireRateLimiting("ai");
        app.MapGet("/chat/{conversationId:guid}", GetChatHistoryAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> SearchAsync(
        string query,
        int? topK,
        ClaimsPrincipal user,
        ISemanticSearchService searchService,
        IOptions<SearchOptions> options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.BadRequest(new ErrorResponse("Query is required."));
        }

        var userId = GetUserId(user);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var limit = NormalizeTopK(topK, options.Value.TopK);
        IReadOnlyCollection<RetrievedChunk> chunks;
        try
        {
            chunks = await searchService.SearchAsync(userId.Value, query.Trim(), limit, cancellationToken);
        }
        catch (AiServiceException)
        {
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

        return Results.Ok(new SearchResponse(chunks.Select(chunk => ToSearchResult(chunk, options.Value.SnippetLength)).ToList()));
    }

    private static async Task<IResult> ChatAsync(
        ChatRequest request,
        ClaimsPrincipal user,
        AmanahDriveDbContext dbContext,
        ISemanticSearchService searchService,
        IAiProcessingClient aiClient,
        IOptions<SearchOptions> options,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Results.BadRequest(new ErrorResponse("Question is required."));
        }

        var userId = GetUserId(user);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var now = DateTimeOffset.UtcNow;
        var conversation = request.ConversationId is null
            ? new Conversation
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                CreatedAt = now,
                UpdatedAt = now
            }
            : await dbContext.Conversations
                .SingleOrDefaultAsync(conversation => conversation.Id == request.ConversationId && conversation.UserId == userId, cancellationToken);

        if (conversation is null)
        {
            return Results.NotFound();
        }

        if (request.ConversationId is null)
        {
            await dbContext.Conversations.AddAsync(conversation, cancellationToken);
        }

        var history = await LoadHistoryAsync(dbContext, conversation.Id, options.Value.ChatHistoryMessageLimit, cancellationToken);
        IReadOnlyCollection<RetrievedChunk> retrievedChunks;
        RagAnswerResponse answer;
        try
        {
            retrievedChunks = await searchService.SearchAsync(userId.Value, request.Question.Trim(), options.Value.TopK, cancellationToken);
            answer = await aiClient.AnswerAsync(
                new RagAnswerRequest(
                    request.Question.Trim(),
                    retrievedChunks.Select(chunk => new RagAnswerChunk(chunk.ChunkId.ToString(), chunk.FileName, chunk.Text)).ToList(),
                    history.Select(message => new RagAnswerHistoryMessage(message.Role, message.Content)).ToList()),
                cancellationToken);
        }
        catch (AiServiceException)
        {
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

        var citations = answer.Citations
            .Select(citation => ToChatCitation(citation, retrievedChunks, options.Value.SnippetLength))
            .ToList();

        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = ChatRoles.User,
            Content = request.Question.Trim(),
            CreatedAt = now
        };
        var assistantMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = ChatRoles.Assistant,
            Content = answer.Answer,
            CitationsJson = JsonSerializer.Serialize(citations, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        };

        conversation.UpdatedAt = assistantMessage.CreatedAt;
        await dbContext.ChatMessages.AddRangeAsync([userMessage, assistantMessage], cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ChatResponse(conversation.Id, answer.Answer, citations));
    }

    private static async Task<IResult> GetChatHistoryAsync(
        Guid conversationId,
        int? page,
        int? pageSize,
        ClaimsPrincipal user,
        AmanahDriveDbContext dbContext,
        IOptions<SearchOptions> options,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var conversation = await dbContext.Conversations
            .Where(conversation => conversation.Id == conversationId && conversation.UserId == userId)
            .Select(conversation => new
            {
                conversation.Id,
                conversation.CreatedAt,
                conversation.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (conversation is null)
        {
            return Results.NotFound();
        }

        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize, options.Value.ChatDefaultPageSize, options.Value.ChatMaxPageSize);
        var skip = (normalizedPage - 1) * normalizedPageSize;

        var messages = await dbContext.ChatMessages
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.CreatedAt)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(message => new ChatMessageResponse(
                message.Id,
                message.Role,
                message.Content,
                DeserializeCitations(message.CitationsJson),
                message.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(new ChatHistoryResponse(conversation.Id, conversation.CreatedAt, conversation.UpdatedAt, normalizedPage, normalizedPageSize, messages));
    }

    private static async Task<IReadOnlyCollection<ChatMessage>> LoadHistoryAsync(
        AmanahDriveDbContext dbContext,
        Guid conversationId,
        int limit,
        CancellationToken cancellationToken)
    {
        var messages = await dbContext.ChatMessages
            .Where(message => message.ConversationId == conversationId)
            .OrderByDescending(message => message.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return messages
            .OrderBy(message => message.CreatedAt)
            .ToList();
    }

    private static SearchResult ToSearchResult(RetrievedChunk chunk, int snippetLength) =>
        new(chunk.ChunkId, chunk.FileId, chunk.FileName, chunk.ChunkIndex, CreateSnippet(chunk.Text, snippetLength), chunk.Score);

    private static ChatCitation ToChatCitation(RagCitation citation, IReadOnlyCollection<RetrievedChunk> retrievedChunks, int snippetLength)
    {
        var matchedChunk = Guid.TryParse(citation.Reference, out var chunkId)
            ? retrievedChunks.SingleOrDefault(chunk => chunk.ChunkId == chunkId)
            : null;

        return new ChatCitation(
            matchedChunk?.ChunkId ?? chunkId,
            matchedChunk?.FileId,
            citation.FileName,
            string.IsNullOrWhiteSpace(citation.Snippet)
                ? CreateSnippet(matchedChunk?.Text ?? string.Empty, snippetLength)
                : CreateSnippet(citation.Snippet, snippetLength));
    }

    private static string CreateSnippet(string text, int maxLength)
    {
        var normalized = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..(maxLength - 3)].TrimEnd() + "...";
    }

    private static IReadOnlyCollection<ChatCitation> DeserializeCitations(string? citationsJson)
    {
        if (string.IsNullOrWhiteSpace(citationsJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<IReadOnlyCollection<ChatCitation>>(citationsJson, JsonOptions) ?? [];
    }

    private static int NormalizeTopK(int? requestedTopK, int configuredTopK)
    {
        if (requestedTopK is null)
        {
            return configuredTopK;
        }

        return Math.Clamp(requestedTopK.Value, 1, 25);
    }

    private static int NormalizePage(int? page) =>
        Math.Max(1, page ?? 1);

    private static int NormalizePageSize(int? pageSize, int defaultPageSize, int maxPageSize)
    {
        if (pageSize is null)
        {
            return defaultPageSize;
        }

        return Math.Clamp(pageSize.Value, 1, maxPageSize);
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static IResult? ValidateRequest<TRequest>(TRequest request)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(request!);
        if (Validator.TryValidateObject(request!, context, validationResults, validateAllProperties: true))
        {
            return null;
        }

        return Results.ValidationProblem(validationResults.ToDictionary(
            result => result.MemberNames.FirstOrDefault() ?? string.Empty,
            result => new[] { result.ErrorMessage ?? "Invalid value." }));
    }
}

public static class ChatRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
}

public sealed record SearchResponse(IReadOnlyCollection<SearchResult> Results);

public sealed record SearchResult(Guid ChunkId, Guid FileId, string FileName, int ChunkIndex, string Snippet, double Score);

public sealed record ChatRequest([Required, MaxLength(4000)] string Question, Guid? ConversationId);

public sealed record ChatResponse(Guid ConversationId, string Answer, IReadOnlyCollection<ChatCitation> Citations);

public sealed record ChatCitation(Guid ChunkId, Guid? FileId, string FileName, string Snippet);

public sealed record ChatHistoryResponse(Guid ConversationId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Page, int PageSize, IReadOnlyCollection<ChatMessageResponse> Messages);

public sealed record ChatMessageResponse(Guid Id, string Role, string Content, IReadOnlyCollection<ChatCitation> Citations, DateTimeOffset CreatedAt);
