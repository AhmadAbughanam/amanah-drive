using AmanahDrive.Api.Modules.Drive.Endpoints;

namespace AmanahDrive.Api.Modules.Drive.Services;

public interface IDriveService
{
    Task<DriveOperationResult<FolderContentsResponse>> ListFolderContentsAsync(
        Guid userId,
        Guid? parentFolderId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken);

    Task<DriveOperationResult<FolderResponse>> CreateFolderAsync(
        Guid userId,
        CreateFolderRequest request,
        CancellationToken cancellationToken);

    Task<DriveOperationResult<FolderResponse>> RenameFolderAsync(
        Guid userId,
        Guid folderId,
        string name,
        CancellationToken cancellationToken);

    Task<DriveOperationResult> DeleteFolderAsync(Guid userId, Guid folderId, CancellationToken cancellationToken);

    Task<DriveOperationResult<FileItemResponse>> UploadFileAsync(
        Guid userId,
        UploadFileCommand command,
        CancellationToken cancellationToken);

    Task<DriveOperationResult<FileItemResponse>> AddYouTubeAsync(
        Guid userId,
        AddYouTubeCommand command,
        CancellationToken cancellationToken);

    Task<FileReadResult> OpenFileReadAsync(Guid userId, Guid fileId, CancellationToken cancellationToken);

    Task<DriveOperationResult<FileItemResponse>> RenameFileAsync(
        Guid userId,
        Guid fileId,
        string name,
        CancellationToken cancellationToken);

    Task<DriveOperationResult<FileItemResponse>> MoveFileAsync(
        Guid userId,
        Guid fileId,
        Guid? folderId,
        CancellationToken cancellationToken);

    Task<DriveOperationResult> DeleteFileAsync(Guid userId, Guid fileId, CancellationToken cancellationToken);

    Task<DriveOperationResult<FileItemResponse>> CopyFileAsync(
        Guid userId,
        CopyFileCommand command,
        CancellationToken cancellationToken);
}

public enum DriveOperationStatus
{
    Success,
    NotFound,
    Conflict,
    Invalid,
    PayloadTooLarge
}

public sealed record DriveOperationResult(DriveOperationStatus Status, string? ErrorMessage = null)
{
    public static DriveOperationResult Success() => new(DriveOperationStatus.Success);

    public static DriveOperationResult NotFound() => new(DriveOperationStatus.NotFound);
}

public sealed record DriveOperationResult<T>(DriveOperationStatus Status, T? Value = default, string? ErrorMessage = null)
{
    public static DriveOperationResult<T> Success(T value) => new(DriveOperationStatus.Success, value);

    public static DriveOperationResult<T> NotFound() => new(DriveOperationStatus.NotFound);

    public static DriveOperationResult<T> Conflict(string errorMessage) => new(DriveOperationStatus.Conflict, default, errorMessage);

    public static DriveOperationResult<T> Invalid(string errorMessage) => new(DriveOperationStatus.Invalid, default, errorMessage);

    public static DriveOperationResult<T> PayloadTooLarge() => new(DriveOperationStatus.PayloadTooLarge);
}

public sealed record UploadFileCommand(
    string FileName,
    string ContentType,
    long Length,
    Guid? FolderId,
    Stream Content);

public sealed record CopyFileCommand(Guid SourceFileId, Guid? DestinationFolderId, string Name);

public sealed record AddYouTubeCommand(string Url, Guid? FolderId);

public sealed record FileReadResult(FileReadResponse? File, string? RedirectUrl = null);

public sealed record FileReadResponse(Stream Content, string ContentType, string FileName);
