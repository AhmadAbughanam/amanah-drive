using AmanahDrive.Api.Shared.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace AmanahDrive.Api.Modules.Processing.Search;

public sealed class ChunkSearchRepository(AmanahDriveDbContext dbContext) : IChunkSearchRepository
{
    public async Task<IReadOnlyCollection<RetrievedChunk>> SearchAsync(Guid userId, float[] queryEmbedding, int topK, CancellationToken cancellationToken)
    {
        var queryVector = new Vector(queryEmbedding);
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
