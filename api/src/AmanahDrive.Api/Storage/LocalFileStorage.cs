using System.Security.Cryptography;
using AmanahDrive.Api.Options;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Storage;

public sealed class LocalFileStorage(IOptions<DriveOptions> options) : IFileStorage
{
    private readonly string _rootPath = Path.GetFullPath(options.Value.StorageRoot);

    public async Task<StoredFileResult> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootPath);

        var id = Guid.NewGuid().ToString("N");
        var storageKey = Path.Combine(id[..2], id);
        var targetPath = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        await using var target = File.Create(targetPath);
        using var sha256 = SHA256.Create();
        var buffer = new byte[81920];
        long size = 0;

        while (true)
        {
            var read = await content.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            sha256.TransformBlock(buffer, 0, read, null, 0);
            size += read;
        }

        sha256.TransformFinalBlock([], 0, 0);
        return new StoredFileResult(storageKey.Replace('\\', '/'), size, Convert.ToHexString(sha256.Hash!));
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        var parts = storageKey.Split('/', '\\', StringSplitOptions.RemoveEmptyEntries);
        var path = Path.GetFullPath(Path.Combine([_rootPath, .. parts]));

        var relativePath = Path.GetRelativePath(_rootPath, path);
        if (relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Storage key resolves outside the configured storage root.");
        }

        return path;
    }
}
