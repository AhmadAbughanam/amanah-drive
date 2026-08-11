namespace AmanahDrive.Api.Ai;

public interface IAiProcessingClient
{
    Task<ExtractResponse> ExtractAsync(string fileName, string contentType, Stream fileStream, CancellationToken cancellationToken);

    Task<ChunkResponse> ChunkAsync(string text, int chunkSize, int overlap, CancellationToken cancellationToken);

    Task<EmbedResponse> EmbedAsync(IReadOnlyCollection<string> texts, CancellationToken cancellationToken);
}

public sealed record ExtractResponse(string Text, string ContentType, int CharacterCount);

public sealed record ChunkResponse(IReadOnlyCollection<ChunkDto> Chunks);

public sealed record ChunkDto(int Index, string Text, int StartOffset, int EndOffset);

public sealed record EmbedResponse(string Model, int Dimension, IReadOnlyCollection<float[]> Embeddings);
