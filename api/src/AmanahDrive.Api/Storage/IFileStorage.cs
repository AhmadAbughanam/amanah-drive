namespace AmanahDrive.Api.Storage;

public interface IFileStorage
{
    Task<StoredFileResult> SaveAsync(Stream content, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed record StoredFileResult(string StorageKey, long SizeBytes, string ChecksumSha256);
