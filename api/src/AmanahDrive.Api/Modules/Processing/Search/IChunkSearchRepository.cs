namespace AmanahDrive.Api.Modules.Processing.Search;

public interface IChunkSearchRepository
{
    Task<IReadOnlyCollection<RetrievedChunk>> SearchAsync(Guid userId, float[] queryEmbedding, int topK, CancellationToken cancellationToken);
}

public sealed record RetrievedChunk(Guid ChunkId, Guid FileId, string FileName, int ChunkIndex, string Text, double Score);
