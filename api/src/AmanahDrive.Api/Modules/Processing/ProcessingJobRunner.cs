using System.Data;
using AmanahDrive.Api.Shared.Infrastructure.Ai;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using AmanahDrive.Api.Modules.Drive.Storage;
using AmanahDrive.Api.Modules.Processing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;

namespace AmanahDrive.Api.Modules.Processing;

public sealed class ProcessingJobRunner(
    AmanahDriveDbContext dbContext,
    IFileStorage storage,
    IAiProcessingClient aiClient,
    IOptions<AiServiceOptions> options,
    ILogger<ProcessingJobRunner> logger)
{
    private const int EmbeddingDimension = 384;
    private readonly AiServiceOptions _options = options.Value;

    public async Task<bool> ProcessNextPendingJobAsync(CancellationToken cancellationToken)
    {
        var jobId = await ClaimNextPendingJobAsync(cancellationToken);
        if (jobId is null)
        {
            return false;
        }

        var job = await dbContext.ProcessingJobs
            .Include(job => job.FileItem)
            .SingleOrDefaultAsync(job => job.Id == jobId.Value, cancellationToken);

        if (job is null)
        {
            logger.LogWarning("Claimed processing job {JobId} was not found after claim", jobId);
            return false;
        }

        await ProcessJobAsync(job, cancellationToken);
        return true;
    }

    private async Task<Guid?> ClaimNextPendingJobAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE processing_jobs
                SET "Status" = @processingStatus,
                    "StartedAt" = @now,
                    "UpdatedAt" = @now
                WHERE "Id" = (
                    SELECT "Id"
                    FROM processing_jobs
                    WHERE "Status" = @pendingStatus
                    ORDER BY "CreatedAt"
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING "Id";
                """;

            AddParameter(command, "processingStatus", ProcessingJobStatus.Processing.ToString());
            AddParameter(command, "pendingStatus", ProcessingJobStatus.Pending.ToString());
            AddParameter(command, "now", DateTimeOffset.UtcNow);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is Guid jobId ? jobId : null;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task ProcessJobAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        try
        {
            await using var fileStream = await storage.OpenReadAsync(job.FileItem.StorageKey, cancellationToken);
            var extracted = await aiClient.ExtractAsync(job.FileItem.OriginalFileName, job.FileItem.ContentType, fileStream, cancellationToken);
            var chunks = await aiClient.ChunkAsync(extracted.Text, _options.ChunkSize, _options.ChunkOverlap, cancellationToken);

            if (chunks.Chunks.Count == 0)
            {
                job.Status = ProcessingJobStatus.Completed;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var embeddings = await aiClient.EmbedAsync(chunks.Chunks.Select(chunk => chunk.Text).ToList(), cancellationToken);
            if (embeddings.Dimension != EmbeddingDimension || embeddings.Embeddings.Count != chunks.Chunks.Count)
            {
                throw new AiServiceException("AI service returned embeddings that do not match chunk count or expected dimension.");
            }

            dbContext.DocumentChunks.RemoveRange(dbContext.DocumentChunks.Where(chunk => chunk.FileItemId == job.FileItemId));

            var now = DateTimeOffset.UtcNow;
            foreach (var pair in chunks.Chunks.Zip(embeddings.Embeddings))
            {
                var chunk = pair.First;
                var embedding = pair.Second;
                if (embedding.Length != EmbeddingDimension)
                {
                    throw new AiServiceException("AI service returned an embedding with an unexpected dimension.");
                }

                await dbContext.DocumentChunks.AddAsync(new DocumentChunk
                {
                    Id = Guid.NewGuid(),
                    FileItemId = job.FileItemId,
                    ChunkIndex = chunk.Index,
                    Text = chunk.Text,
                    StartOffset = chunk.StartOffset,
                    EndOffset = chunk.EndOffset,
                    Embedding = new Vector(embedding),
                    CreatedAt = now
                }, cancellationToken);
            }

            job.Status = ProcessingJobStatus.Completed;
            job.ErrorMessage = null;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Document processing job {JobId} failed", job.Id);
            job.Status = ProcessingJobStatus.Failed;
            job.ErrorMessage = ex.Message.Length > 2048 ? ex.Message[..2048] : ex.Message;
            job.FailedAt = DateTimeOffset.UtcNow;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
