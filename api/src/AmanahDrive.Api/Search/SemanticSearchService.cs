using AmanahDrive.Api.Ai;
using AmanahDrive.Api.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace AmanahDrive.Api.Search;

public sealed class SemanticSearchService(AmanahDriveDbContext dbContext, IAiProcessingClient aiClient) : ISemanticSearchService
{
    private const int EmbeddingDimension = 384;

    public async Task<IReadOnlyCollection<RetrievedChunk>> SearchAsync(Guid userId, string query, int topK, CancellationToken cancellationToken)
    {
        var embeddingResponse = await aiClient.EmbedAsync([query], cancellationToken);
        var embedding = embeddingResponse.Embeddings.SingleOrDefault()
            ?? throw new AiServiceException("AI service did not return a query embedding.");

        if (embeddingResponse.Dimension != EmbeddingDimension || embedding.Length != EmbeddingDimension)
        {
            throw new AiServiceException("AI service returned an embedding with an unexpected dimension.");
        }

        var queryVector = new Vector(embedding);
        var matches = await dbContext.DocumentChunks
            .Where(chunk => chunk.FileItem.UserId == userId)
            .OrderBy(chunk => chunk.Embedding.CosineDistance(queryVector))
            .Take(topK)
            .Select(chunk => new
            {
                chunk.Id,
                chunk.FileItemId,
                chunk.FileItem.OriginalFileName,
                chunk.ChunkIndex,
                chunk.Text,
                Distance = chunk.Embedding.CosineDistance(queryVector)
            })
            .ToListAsync(cancellationToken);

        return matches
            .Select(match => new RetrievedChunk(
                match.Id,
                match.FileItemId,
                match.OriginalFileName,
                match.ChunkIndex,
                match.Text,
                Math.Max(0, 1 - match.Distance)))
            .ToList();
    }
}
