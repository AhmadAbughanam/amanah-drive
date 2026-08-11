using AmanahDrive.Api.Modules.Processing.Search;

namespace AmanahDrive.Api.Modules.SearchChat.Search;

public interface ISemanticSearchService
{
    Task<IReadOnlyCollection<RetrievedChunk>> SearchAsync(Guid userId, string query, int topK, CancellationToken cancellationToken);
}
