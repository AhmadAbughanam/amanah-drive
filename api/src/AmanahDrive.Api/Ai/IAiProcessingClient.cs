namespace AmanahDrive.Api.Ai;

public interface IAiProcessingClient
{
    Task<ExtractResponse> ExtractAsync(string fileName, string contentType, Stream fileStream, CancellationToken cancellationToken);

    Task<ChunkResponse> ChunkAsync(string text, int chunkSize, int overlap, CancellationToken cancellationToken);

    Task<EmbedResponse> EmbedAsync(IReadOnlyCollection<string> texts, CancellationToken cancellationToken);

    Task<RagAnswerResponse> AnswerAsync(RagAnswerRequest request, CancellationToken cancellationToken);
}

public sealed record ExtractResponse(string Text, string ContentType, int CharacterCount);

public sealed record ChunkResponse(IReadOnlyCollection<ChunkDto> Chunks);

public sealed record ChunkDto(int Index, string Text, int StartOffset, int EndOffset);

public sealed record EmbedResponse(string Model, int Dimension, IReadOnlyCollection<float[]> Embeddings);

public sealed record RagAnswerRequest(string Question, IReadOnlyCollection<RagAnswerChunk> Chunks, IReadOnlyCollection<RagAnswerHistoryMessage> History);

public sealed record RagAnswerChunk(string Reference, string FileName, string Text);

public sealed record RagAnswerHistoryMessage(string Role, string Content);

public sealed record RagAnswerResponse(string Answer, string Model, IReadOnlyCollection<RagCitation> Citations);

public sealed record RagCitation(string Reference, string FileName, string Snippet);
