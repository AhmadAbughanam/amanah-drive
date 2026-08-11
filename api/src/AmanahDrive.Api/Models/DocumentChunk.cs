using Pgvector;

namespace AmanahDrive.Api.Models;

public sealed class DocumentChunk
{
    public Guid Id { get; set; }

    public Guid FileItemId { get; set; }

    public FileItem FileItem { get; set; } = null!;

    public int ChunkIndex { get; set; }

    public required string Text { get; set; }

    public int StartOffset { get; set; }

    public int EndOffset { get; set; }

    public Vector Embedding { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
