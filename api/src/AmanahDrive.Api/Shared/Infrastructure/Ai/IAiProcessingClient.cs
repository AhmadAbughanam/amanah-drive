namespace AmanahDrive.Api.Shared.Infrastructure.Ai;

public interface IAiProcessingClient
{
    Task<ExtractResponse> ExtractAsync(string fileName, string contentType, Stream fileStream, CancellationToken cancellationToken);

    Task<ChunkResponse> ChunkAsync(string text, int chunkSize, int overlap, CancellationToken cancellationToken);

    Task<EmbedResponse> EmbedAsync(IReadOnlyCollection<string> texts, CancellationToken cancellationToken);

    Task<RagAnswerResponse> AnswerAsync(RagAnswerRequest request, CancellationToken cancellationToken);

    Task<AgentCompletionResponse> CompleteAgentAsync(AgentCompletionRequest request, CancellationToken cancellationToken);
}

public sealed record ExtractResponse(string Text, string ContentType, int CharacterCount);

public sealed record ChunkResponse(IReadOnlyCollection<ChunkDto> Chunks);

public sealed record ChunkDto(int Index, string Text, int StartOffset, int EndOffset);

public sealed record EmbedResponse(string Model, int Dimension, IReadOnlyCollection<float[]> Embeddings, AiModelUsage? Usage = null);

public sealed record RagAnswerRequest(string Question, IReadOnlyCollection<RagAnswerChunk> Chunks, IReadOnlyCollection<RagAnswerHistoryMessage> History);

public sealed record RagAnswerChunk(string FileName, string Text);

public sealed record RagAnswerHistoryMessage(string Role, string Content);

public sealed record RagAnswerResponse(string Answer, string Model, IReadOnlyCollection<RagCitation> Citations, AiModelUsage? Usage = null);

public sealed record RagCitation(string Reference, string FileName, string Snippet);

public sealed record AgentCompletionRequest(IReadOnlyCollection<AgentChatMessage> Messages, IReadOnlyCollection<AgentToolDefinition> Tools);

public sealed record AgentChatMessage(string Role, string? Content, string? ToolCallId = null, IReadOnlyCollection<AgentToolCall>? ToolCalls = null);

public sealed record AgentToolDefinition(string Type, AgentToolFunction Function);

public sealed record AgentToolFunction(string Name, string Description, System.Text.Json.JsonElement Parameters);

public sealed record AgentToolCall(string Id, string Type, AgentToolCallFunction Function);

public sealed record AgentToolCallFunction(string Name, string Arguments);

public sealed record AgentCompletionResponse(AgentChatMessage Message, string Model, AiModelUsage? Usage = null);

public sealed record AiModelUsage(string Provider, int? InputTokens, int? OutputTokens);
