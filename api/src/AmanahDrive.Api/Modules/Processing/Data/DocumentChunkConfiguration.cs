using AmanahDrive.Api.Modules.Processing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmanahDrive.Api.Modules.Processing.Data;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> entity)
    {
        entity.ToTable("document_chunks");
        entity.HasKey(chunk => chunk.Id);
        entity.Property(chunk => chunk.Text).IsRequired();
        entity.Property(chunk => chunk.Embedding).HasColumnType("vector(384)").IsRequired();
        entity.HasIndex(chunk => chunk.FileItemId);
        entity.HasIndex(chunk => new { chunk.FileItemId, chunk.ChunkIndex }).IsUnique();
        entity.HasOne(chunk => chunk.FileItem)
            .WithMany(file => file.DocumentChunks)
            .HasForeignKey(chunk => chunk.FileItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
