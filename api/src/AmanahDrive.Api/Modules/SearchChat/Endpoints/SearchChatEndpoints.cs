using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using AmanahDrive.Api.Modules.Processing.Search;
using AmanahDrive.Api.Modules.SearchChat.Models;
using AmanahDrive.Api.Modules.SearchChat.Events;
using AmanahDrive.Api.Modules.SearchChat.Options;
using AmanahDrive.Api.Modules.SearchChat.Search;
using AmanahDrive.Api.Shared.Infrastructure.Ai;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using AmanahDrive.Api.Shared.Infrastructure.Http;
using AmanahDrive.Api.Shared.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Modules.SearchChat.Endpoints;

public static class SearchChatEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapSearchChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/search", SearchAsync)
            .RequireAuthorization()
            .RequireRateLimiting("ai")
            .WithTags("Search & Chat")
            .WithSummary("Run semantic search over processed document chunks.")
            .Produces<SearchResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status502BadGateway);

        app.MapPost("/chat", ChatAsync)
            .RequireAuthorization()
            .RequireRateLimiting("ai")
            .WithTags("Search & Chat")
            .WithSummary("Ask a grounded AI question over retrieved document chunks.")
            .Produces<ChatResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status502BadGateway)
            .ProducesValidationProblem();

        app.MapGet("/chat/{conversationId:guid}", GetChatHistoryAsync)
            .RequireAuthorization()
            .WithTags("Search & Chat")
            .WithSummary("Return paginated chat history for a conversation.")
            .Produces<ChatHistoryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
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
        IDomainEventDispatcher eventDispatcher,
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
        IReadOnlyList<RetrievedChunk> retrievedChunks;
        RagAnswerResponse answer;
        try
        {
            retrievedChunks = (await searchService.SearchAsync(userId.Value, request.Question.Trim(), options.Value.TopK, cancellationToken)).ToList();
            answer = await aiClient.AnswerAsync(
                new RagAnswerRequest(
                    request.Question.Trim(),
                    retrievedChunks.Select(chunk => new RagAnswerChunk(chunk.FileName, chunk.Text)).ToList(),
                    history.Select(message => new RagAnswerHistoryMessage(message.Role, message.Content)).ToList()),
                cancellationToken);
        }
        catch (AiServiceException)
        {
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

        var citations = CreateChatCitations(answer.Answer, retrievedChunks, options.Value.SnippetLength);

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
        await eventDispatcher.PublishAsync(
            new ChatAnsweredEvent(conversation.Id, request.Question.Trim(), assistantMessage.CreatedAt),
            cancellationToken);

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

    private static IReadOnlyList<ChatCitation> CreateChatCitations(string answer, IReadOnlyList<RetrievedChunk> retrievedChunks, int snippetLength)
    {
        return FindCitationReferences(answer, retrievedChunks.Count)
            .Select(reference =>
            {
                var chunk = retrievedChunks[reference - 1];
                return new ChatCitation(
                    reference,
                    chunk.ChunkId,
                    chunk.FileId,
                    chunk.FileName,
                    CreateSnippet(chunk.Text, snippetLength),
                    chunk.ChunkIndex,
                    chunk.Score);
            })
            .ToList();
    }

    private static IReadOnlyList<int> FindCitationReferences(string answer, int chunkCount)
    {
        var references = new List<int>();
        var seen = new HashSet<int>();
        var insideCode = false;

        for (var index = 0; index < answer.Length; index++)
        {
            if (answer[index] == '`' && (index == 0 || answer[index - 1] != '\\'))
            {
                while (index + 1 < answer.Length && answer[index + 1] == '`')
                {
                    index++;
                }

                insideCode = !insideCode;
                continue;
            }

            if (insideCode || answer[index] != '[')
            {
                continue;
            }

            var closingBracket = answer.IndexOf(']', index + 1);
            if (closingBracket < 0)
            {
                break;
            }

            var candidate = answer.AsSpan(index + 1, closingBracket - index - 1);
            if (int.TryParse(candidate, NumberStyles.None, CultureInfo.InvariantCulture, out var reference)
                && reference >= 1
                && reference <= chunkCount
                && seen.Add(reference))
            {
                references.Add(reference);
            }

            index = closingBracket;
        }

        return references;
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

        var citations = JsonSerializer.Deserialize<IReadOnlyCollection<ChatCitation>>(citationsJson, JsonOptions) ?? [];
        return citations
            .Select((citation, index) => citation.Reference > 0 ? citation : citation with { Reference = index + 1 })
            .ToList();
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

public sealed record ChatCitation(
    int Reference,
    Guid ChunkId,
    Guid? FileId,
    string FileName,
    string Snippet,
    int? ChunkIndex = null,
    double? RelevanceScore = null);

public sealed record ChatHistoryResponse(Guid ConversationId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Page, int PageSize, IReadOnlyCollection<ChatMessageResponse> Messages);

public sealed record ChatMessageResponse(Guid Id, string Role, string Content, IReadOnlyCollection<ChatCitation> Citations, DateTimeOffset CreatedAt);
