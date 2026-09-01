using AmanahDrive.Api.Modules.Drive.Endpoints;
using AmanahDrive.Api.Modules.Drive.Events;
using AmanahDrive.Api.Modules.Drive.Models;
using AmanahDrive.Api.Modules.Drive.Options;
using AmanahDrive.Api.Modules.Drive.Storage;
using AmanahDrive.Api.Modules.Processing.Models;
using AmanahDrive.Api.Shared.DomainEvents;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Modules.Drive.Services;

public sealed class DriveService(
    AmanahDriveDbContext dbContext,
    IFileStorage storage,
    IDomainEventDispatcher eventDispatcher,
    IOptions<DriveOptions> options) : IDriveService
{
    public async Task<DriveOperationResult<FolderContentsResponse>> ListFolderContentsAsync(
        Guid userId,
        Guid? parentFolderId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (parentFolderId is not null && !await FolderBelongsToUserAsync(parentFolderId.Value, userId, cancellationToken))
        {
            return DriveOperationResult<FolderContentsResponse>.NotFound();
        }

        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize, options.Value.DefaultPageSize, options.Value.MaxPageSize);
        var skip = (normalizedPage - 1) * normalizedPageSize;

        var folders = await dbContext.Folders
            .Where(folder => folder.UserId == userId && folder.ParentFolderId == parentFolderId)
            .OrderBy(folder => folder.Name)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(folder => new FolderResponse(folder.Id, folder.Name, folder.ParentFolderId, folder.CreatedAt, folder.UpdatedAt))
            .ToListAsync(cancellationToken);

        var files = await dbContext.FileItems
            .Where(file => file.UserId == userId && file.FolderId == parentFolderId)
            .OrderBy(file => file.OriginalFileName)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(file => new FileItemResponse(file.Id, file.FolderId, file.OriginalFileName, file.ContentType, file.SizeBytes, file.ChecksumSha256, file.ProcessingJob == null ? null : file.ProcessingJob.Id, file.CreatedAt, file.UpdatedAt))
            .ToListAsync(cancellationToken);

        return DriveOperationResult<FolderContentsResponse>.Success(
            new FolderContentsResponse(parentFolderId, normalizedPage, normalizedPageSize, folders, files));
    }

    public async Task<DriveOperationResult<FolderResponse>> CreateFolderAsync(Guid userId, CreateFolderRequest request, CancellationToken cancellationToken)
    {
        var nameError = ValidateName(request.Name);
        if (nameError is not null)
        {
            return DriveOperationResult<FolderResponse>.Invalid(nameError);
        }

        if (request.ParentFolderId is not null && !await FolderBelongsToUserAsync(request.ParentFolderId.Value, userId, cancellationToken))
        {
            return DriveOperationResult<FolderResponse>.NotFound();
        }

        if (await FolderNameExistsAsync(userId, request.ParentFolderId, request.Name, cancellationToken))
        {
            return DriveOperationResult<FolderResponse>.Conflict("A folder with that name already exists in this location.");
        }

        var now = DateTimeOffset.UtcNow;
        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            ParentFolderId = request.ParentFolderId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await dbContext.Folders.AddAsync(folder, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return DriveOperationResult<FolderResponse>.Success(ToFolderResponse(folder));
    }

    public async Task<DriveOperationResult<FolderResponse>> RenameFolderAsync(Guid userId, Guid folderId, string name, CancellationToken cancellationToken)
    {
        var nameError = ValidateName(name);
        if (nameError is not null)
        {
            return DriveOperationResult<FolderResponse>.Invalid(nameError);
        }

        var folder = await dbContext.Folders.SingleOrDefaultAsync(folder => folder.Id == folderId && folder.UserId == userId, cancellationToken);
        if (folder is null)
        {
            return DriveOperationResult<FolderResponse>.NotFound();
        }

        if (await FolderNameExistsAsync(userId, folder.ParentFolderId, name, cancellationToken, folder.Id))
        {
            return DriveOperationResult<FolderResponse>.Conflict("A folder with that name already exists in this location.");
        }

        folder.Name = name.Trim();
        folder.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return DriveOperationResult<FolderResponse>.Success(ToFolderResponse(folder));
    }

    public async Task<DriveOperationResult> DeleteFolderAsync(Guid userId, Guid folderId, CancellationToken cancellationToken)
    {
        var folder = await dbContext.Folders.SingleOrDefaultAsync(folder => folder.Id == folderId && folder.UserId == userId, cancellationToken);
        if (folder is null)
        {
            return DriveOperationResult.NotFound();
        }

        var folderIds = await GetDescendantFolderIdsAsync(userId, folderId, cancellationToken);
        var files = await dbContext.FileItems
            .Where(file => file.UserId == userId && file.FolderId != null && folderIds.Contains(file.FolderId.Value))
            .ToListAsync(cancellationToken);

        foreach (var file in files)
        {
            await storage.DeleteAsync(file.StorageKey, cancellationToken);
        }

        dbContext.Folders.Remove(folder);
        await dbContext.SaveChangesAsync(cancellationToken);
        return DriveOperationResult.Success();
    }

    public async Task<DriveOperationResult<FileItemResponse>> UploadFileAsync(Guid userId, UploadFileCommand command, CancellationToken cancellationToken)
    {
        if (command.Length == 0)
        {
            return DriveOperationResult<FileItemResponse>.Invalid("File is empty.");
        }

        if (command.Length > options.Value.MaxFileSizeBytes)
        {
            return DriveOperationResult<FileItemResponse>.PayloadTooLarge();
        }

        var nameError = ValidateName(command.FileName);
        if (nameError is not null)
        {
            return DriveOperationResult<FileItemResponse>.Invalid(nameError);
        }

        if (!IsAllowedContentType(command.ContentType, options.Value.AllowedContentTypes))
        {
            return DriveOperationResult<FileItemResponse>.Invalid("File content type is not allowed.");
        }

        if (command.FolderId is not null && !await FolderBelongsToUserAsync(command.FolderId.Value, userId, cancellationToken))
        {
            return DriveOperationResult<FileItemResponse>.NotFound();
        }

        if (await FileNameExistsAsync(userId, command.FolderId, command.FileName, cancellationToken))
        {
            return DriveOperationResult<FileItemResponse>.Conflict("A file with that name already exists in this location.");
        }

        var storedFile = await storage.SaveAsync(command.Content, cancellationToken);
        return await CreateStoredFileAsync(
            userId,
            command.FolderId,
            command.FileName.Trim(),
            NormalizeContentType(command.ContentType),
            storedFile,
            cancellationToken);
    }

    public async Task<FileReadResult> OpenFileReadAsync(Guid userId, Guid fileId, CancellationToken cancellationToken)
    {
        var fileItem = await dbContext.FileItems.SingleOrDefaultAsync(file => file.Id == fileId && file.UserId == userId, cancellationToken);
        if (fileItem is null)
        {
            return new FileReadResult(null);
        }

        var content = await storage.OpenReadAsync(fileItem.StorageKey, cancellationToken);
        return new FileReadResult(new FileReadResponse(content, fileItem.ContentType, fileItem.OriginalFileName));
    }

    public async Task<DriveOperationResult<FileItemResponse>> RenameFileAsync(Guid userId, Guid fileId, string name, CancellationToken cancellationToken)
    {
        var nameError = ValidateName(name);
        if (nameError is not null)
        {
            return DriveOperationResult<FileItemResponse>.Invalid(nameError);
        }

        var fileItem = await dbContext.FileItems.SingleOrDefaultAsync(file => file.Id == fileId && file.UserId == userId, cancellationToken);
        if (fileItem is null)
        {
            return DriveOperationResult<FileItemResponse>.NotFound();
        }

        if (await FileNameExistsAsync(userId, fileItem.FolderId, name, cancellationToken, fileItem.Id))
        {
            return DriveOperationResult<FileItemResponse>.Conflict("A file with that name already exists in this location.");
        }

        fileItem.OriginalFileName = name.Trim();
        fileItem.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return DriveOperationResult<FileItemResponse>.Success(ToFileItemResponse(fileItem));
    }

    public async Task<DriveOperationResult<FileItemResponse>> MoveFileAsync(Guid userId, Guid fileId, Guid? folderId, CancellationToken cancellationToken)
    {
        var fileItem = await dbContext.FileItems.SingleOrDefaultAsync(file => file.Id == fileId && file.UserId == userId, cancellationToken);
        if (fileItem is null)
        {
            return DriveOperationResult<FileItemResponse>.NotFound();
        }

        if (folderId is not null && !await FolderBelongsToUserAsync(folderId.Value, userId, cancellationToken))
        {
            return DriveOperationResult<FileItemResponse>.NotFound();
        }

        if (await FileNameExistsAsync(userId, folderId, fileItem.OriginalFileName, cancellationToken, fileItem.Id))
        {
            return DriveOperationResult<FileItemResponse>.Conflict("A file with that name already exists in the destination folder.");
        }

        fileItem.FolderId = folderId;
        fileItem.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return DriveOperationResult<FileItemResponse>.Success(ToFileItemResponse(fileItem));
    }

    public async Task<DriveOperationResult> DeleteFileAsync(Guid userId, Guid fileId, CancellationToken cancellationToken)
    {
        var fileItem = await dbContext.FileItems.SingleOrDefaultAsync(file => file.Id == fileId && file.UserId == userId, cancellationToken);
        if (fileItem is null)
        {
            return DriveOperationResult.NotFound();
        }

        await storage.DeleteAsync(fileItem.StorageKey, cancellationToken);
        dbContext.FileItems.Remove(fileItem);
        await dbContext.SaveChangesAsync(cancellationToken);
        return DriveOperationResult.Success();
    }

    public async Task<DriveOperationResult<FileItemResponse>> CopyFileAsync(Guid userId, CopyFileCommand command, CancellationToken cancellationToken)
    {
        var nameError = ValidateName(command.Name);
        if (nameError is not null)
        {
            return DriveOperationResult<FileItemResponse>.Invalid(nameError);
        }

        var sourceFile = await dbContext.FileItems.SingleOrDefaultAsync(file => file.Id == command.SourceFileId && file.UserId == userId, cancellationToken);
        if (sourceFile is null)
        {
            return DriveOperationResult<FileItemResponse>.NotFound();
        }

        if (command.DestinationFolderId is not null && !await FolderBelongsToUserAsync(command.DestinationFolderId.Value, userId, cancellationToken))
        {
            return DriveOperationResult<FileItemResponse>.NotFound();
        }

        if (await FileNameExistsAsync(userId, command.DestinationFolderId, command.Name, cancellationToken))
        {
            return DriveOperationResult<FileItemResponse>.Conflict("A file with that name already exists in this location.");
        }

        await using var sourceContent = await storage.OpenReadAsync(sourceFile.StorageKey, cancellationToken);
        var storedFile = await storage.SaveAsync(sourceContent, cancellationToken);
        return await CreateStoredFileAsync(
            userId,
            command.DestinationFolderId,
            command.Name.Trim(),
            sourceFile.ContentType,
            storedFile,
            cancellationToken);
    }

    private async Task<DriveOperationResult<FileItemResponse>> CreateStoredFileAsync(
        Guid userId,
        Guid? folderId,
        string fileName,
        string contentType,
        StoredFileResult storedFile,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var fileItem = new FileItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FolderId = folderId,
            OriginalFileName = fileName,
            StorageKey = storedFile.StorageKey,
            ContentType = contentType,
            SizeBytes = storedFile.SizeBytes,
            ChecksumSha256 = storedFile.ChecksumSha256,
            CreatedAt = now,
            UpdatedAt = now
        };

        await dbContext.FileItems.AddAsync(fileItem, cancellationToken);
        var processingJob = new ProcessingJob
        {
            Id = Guid.NewGuid(),
            FileItemId = fileItem.Id,
            Status = ProcessingJobStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        await dbContext.ProcessingJobs.AddAsync(processingJob, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await eventDispatcher.PublishAsync(new FileUploadedEvent(fileItem.Id, fileItem.OriginalFileName, now), cancellationToken);
        return DriveOperationResult<FileItemResponse>.Success(ToFileItemResponse(fileItem, processingJob.Id));
    }

    private static FolderResponse ToFolderResponse(Folder folder) =>
        new(folder.Id, folder.Name, folder.ParentFolderId, folder.CreatedAt, folder.UpdatedAt);

    private static FileItemResponse ToFileItemResponse(FileItem fileItem, Guid? processingJobId = null) =>
        new(fileItem.Id, fileItem.FolderId, fileItem.OriginalFileName, fileItem.ContentType, fileItem.SizeBytes, fileItem.ChecksumSha256, processingJobId ?? fileItem.ProcessingJob?.Id, fileItem.CreatedAt, fileItem.UpdatedAt);

    private static string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name is required.";
        }

        var trimmed = name.Trim();
        if (trimmed is "." or ".." ||
            trimmed.Contains("..", StringComparison.Ordinal) ||
            trimmed.IndexOfAny(['/', '\\']) >= 0 ||
            Path.GetFileName(trimmed) != trimmed ||
            trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "Name contains invalid path characters.";
        }

        return null;
    }

    private async Task<bool> FolderBelongsToUserAsync(Guid folderId, Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Folders.AnyAsync(folder => folder.Id == folderId && folder.UserId == userId, cancellationToken);

    private async Task<bool> FolderNameExistsAsync(Guid userId, Guid? parentFolderId, string name, CancellationToken cancellationToken, Guid? excludingFolderId = null) =>
        await dbContext.Folders.AnyAsync(
            folder =>
                folder.UserId == userId &&
                folder.ParentFolderId == parentFolderId &&
                folder.Name == name.Trim() &&
                folder.Id != excludingFolderId,
            cancellationToken);

    private async Task<bool> FileNameExistsAsync(Guid userId, Guid? folderId, string fileName, CancellationToken cancellationToken, Guid? excludingFileId = null) =>
        await dbContext.FileItems.AnyAsync(
            file =>
                file.UserId == userId &&
                file.FolderId == folderId &&
                file.OriginalFileName == fileName.Trim() &&
                file.Id != excludingFileId,
            cancellationToken);

    private async Task<HashSet<Guid>> GetDescendantFolderIdsAsync(Guid userId, Guid folderId, CancellationToken cancellationToken)
    {
        var folders = await dbContext.Folders
            .Where(folder => folder.UserId == userId)
            .Select(folder => new { folder.Id, folder.ParentFolderId })
            .ToListAsync(cancellationToken);

        var folderIds = new HashSet<Guid> { folderId };
        var changed = true;

        while (changed)
        {
            changed = false;
            foreach (var folder in folders)
            {
                if (folder.ParentFolderId is not null && folderIds.Contains(folder.ParentFolderId.Value) && folderIds.Add(folder.Id))
                {
                    changed = true;
                }
            }
        }

        return folderIds;
    }

    private static bool IsAllowedContentType(string contentType, IReadOnlyCollection<string> allowedContentTypes) =>
        allowedContentTypes.Contains(NormalizeContentType(contentType), StringComparer.OrdinalIgnoreCase);

    private static string NormalizeContentType(string contentType) =>
        contentType.Split(';', 2)[0].Trim();

    private static int NormalizePage(int? page) =>
        Math.Max(1, page ?? 1);

    private static int NormalizePageSize(int? pageSize, int defaultPageSize, int maxPageSize)
    {
        if (pageSize is null)
        {
            return defaultPageSize;
        }

        return Math.Clamp(pageSize.Value, 1, maxPageSize);
    }
}
