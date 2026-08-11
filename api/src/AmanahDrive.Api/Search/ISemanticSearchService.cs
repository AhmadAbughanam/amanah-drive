namespace AmanahDrive.Api.Search;

public interface ISemanticSearchService
{
    Task<IReadOnlyCollection<RetrievedChunk>> SearchAsync(Guid userId, string query, int topK, CancellationToken cancellationToken);
}

public sealed record RetrievedChunk(Guid ChunkId, Guid FileId, string FileName, int ChunkIndex, string Text, double Score);
