using AmanahDrive.Api.Shared.Infrastructure.Ai;
using AmanahDrive.Api.Modules.Processing.Search;

namespace AmanahDrive.Api.Modules.SearchChat.Search;

public sealed class SemanticSearchService(IAiProcessingClient aiClient, IChunkSearchRepository chunkSearchRepository) : ISemanticSearchService
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

        return await chunkSearchRepository.SearchAsync(userId, embedding, topK, cancellationToken);
    }
}
