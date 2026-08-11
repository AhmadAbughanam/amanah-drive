using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AmanahDrive.Api.Data;
using AmanahDrive.Api.Models;
using AmanahDrive.Api.Options;
using AmanahDrive.Api.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Endpoints;

public static class DriveEndpoints
{
    public static RouteGroupBuilder MapDriveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/drive").RequireAuthorization();

        group.MapGet("/folders", async (
            Guid? parentFolderId,
            int? page,
            int? pageSize,
            ClaimsPrincipal user,
            AmanahDriveDbContext dbContext,
            IOptions<DriveOptions> options,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            if (parentFolderId is not null && !await FolderBelongsToUserAsync(dbContext, parentFolderId.Value, userId.Value, cancellationToken))
            {
                return Results.NotFound();
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

            return Results.Ok(new FolderContentsResponse(parentFolderId, normalizedPage, normalizedPageSize, folders, files));
        });

        group.MapPost("/folders", async (
            CreateFolderRequest request,
            ClaimsPrincipal user,
            AmanahDriveDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateRequest(request);
            if (validation is not null)
            {
                return validation;
            }

            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var nameValidation = ValidateName(request.Name);
            if (nameValidation is not null)
            {
                return nameValidation;
            }

            if (request.ParentFolderId is not null && !await FolderBelongsToUserAsync(dbContext, request.ParentFolderId.Value, userId.Value, cancellationToken))
            {
                return Results.NotFound();
            }

            if (await FolderNameExistsAsync(dbContext, userId.Value, request.ParentFolderId, request.Name, cancellationToken))
            {
                return Results.Conflict(new ErrorResponse("A folder with that name already exists in this location."));
            }

            var now = DateTimeOffset.UtcNow;
            var folder = new Folder
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Name = request.Name.Trim(),
                ParentFolderId = request.ParentFolderId,
                CreatedAt = now,
                UpdatedAt = now
            };

            await dbContext.Folders.AddAsync(folder, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/drive/folders/{folder.Id}", new FolderResponse(folder.Id, folder.Name, folder.ParentFolderId, folder.CreatedAt, folder.UpdatedAt));
        });

        group.MapPatch("/folders/{folderId:guid}/rename", async (
            Guid folderId,
            RenameRequest request,
            ClaimsPrincipal user,
            AmanahDriveDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateRequest(request);
            if (validation is not null)
            {
                return validation;
            }

            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var nameValidation = ValidateName(request.Name);
            if (nameValidation is not null)
            {
                return nameValidation;
            }

            var folder = await dbContext.Folders.SingleOrDefaultAsync(folder => folder.Id == folderId && folder.UserId == userId, cancellationToken);
            if (folder is null)
            {
                return Results.NotFound();
            }

            if (await FolderNameExistsAsync(dbContext, userId.Value, folder.ParentFolderId, request.Name, cancellationToken, folder.Id))
            {
                return Results.Conflict(new ErrorResponse("A folder with that name already exists in this location."));
            }

            folder.Name = request.Name.Trim();
            folder.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new FolderResponse(folder.Id, folder.Name, folder.ParentFolderId, folder.CreatedAt, folder.UpdatedAt));
        });

        group.MapDelete("/folders/{folderId:guid}", async (
            Guid folderId,
            ClaimsPrincipal user,
            AmanahDriveDbContext dbContext,
            IFileStorage storage,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var folder = await dbContext.Folders.SingleOrDefaultAsync(folder => folder.Id == folderId && folder.UserId == userId, cancellationToken);
            if (folder is null)
            {
                return Results.NotFound();
            }

            var folderIds = await GetDescendantFolderIdsAsync(dbContext, userId.Value, folderId, cancellationToken);
            var files = await dbContext.FileItems
                .Where(file => file.UserId == userId && file.FolderId != null && folderIds.Contains(file.FolderId.Value))
                .ToListAsync(cancellationToken);

            foreach (var file in files)
            {
                await storage.DeleteAsync(file.StorageKey, cancellationToken);
            }

            dbContext.Folders.Remove(folder);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        });

        group.MapPost("/files/upload", async (
            [FromForm] IFormFile file,
            [FromForm] Guid? folderId,
            ClaimsPrincipal user,
            AmanahDriveDbContext dbContext,
            IFileStorage storage,
            IOptions<DriveOptions> options,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            if (file.Length == 0)
            {
                return Results.BadRequest(new ErrorResponse("File is empty."));
            }

            if (file.Length > options.Value.MaxFileSizeBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var nameValidation = ValidateName(file.FileName);
            if (nameValidation is not null)
            {
                return nameValidation;
            }

            if (!IsAllowedContentType(file.ContentType, options.Value.AllowedContentTypes))
            {
                return Results.BadRequest(new ErrorResponse("File content type is not allowed."));
            }

            if (folderId is not null && !await FolderBelongsToUserAsync(dbContext, folderId.Value, userId.Value, cancellationToken))
            {
                return Results.NotFound();
            }

            if (await FileNameExistsAsync(dbContext, userId.Value, folderId, file.FileName, cancellationToken))
            {
                return Results.Conflict(new ErrorResponse("A file with that name already exists in this location."));
            }

            await using var stream = file.OpenReadStream();
            var storedFile = await storage.SaveAsync(stream, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var fileItem = new FileItem
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                FolderId = folderId,
                OriginalFileName = file.FileName.Trim(),
                StorageKey = storedFile.StorageKey,
                ContentType = NormalizeContentType(file.ContentType),
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

            return Results.Created($"/drive/files/{fileItem.Id}", new FileItemResponse(fileItem.Id, fileItem.FolderId, fileItem.OriginalFileName, fileItem.ContentType, fileItem.SizeBytes, fileItem.ChecksumSha256, processingJob.Id, fileItem.CreatedAt, fileItem.UpdatedAt));
        }).DisableAntiforgery();

        group.MapGet("/files/{fileId:guid}/download", async (
            Guid fileId,
            ClaimsPrincipal user,
            AmanahDriveDbContext dbContext,
            IFileStorage storage,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var fileItem = await dbContext.FileItems.SingleOrDefaultAsync(file => file.Id == fileId && file.UserId == userId, cancellationToken);
            if (fileItem is null)
            {
                return Results.NotFound();
            }

            var stream = await storage.OpenReadAsync(fileItem.StorageKey, cancellationToken);
            return Results.File(stream, fileItem.ContentType, fileItem.OriginalFileName);
        });

        group.MapPatch("/files/{fileId:guid}/rename", async (
            Guid fileId,
            RenameRequest request,
            ClaimsPrincipal user,
            AmanahDriveDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var validation = ValidateRequest(request);
            if (validation is not null)
            {
                return validation;
            }

            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var nameValidation = ValidateName(request.Name);
            if (nameValidation is not null)
            {
                return nameValidation;
            }

            var fileItem = await dbContext.FileItems.SingleOrDefaultAsync(file => file.Id == fileId && file.UserId == userId, cancellationToken);
            if (fileItem is null)
            {
                return Results.NotFound();
            }

            if (await FileNameExistsAsync(dbContext, userId.Value, fileItem.FolderId, request.Name, cancellationToken, fileItem.Id))
            {
                return Results.Conflict(new ErrorResponse("A file with that name already exists in this location."));
            }

            fileItem.OriginalFileName = request.Name.Trim();
            fileItem.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new FileItemResponse(fileItem.Id, fileItem.FolderId, fileItem.OriginalFileName, fileItem.ContentType, fileItem.SizeBytes, fileItem.ChecksumSha256, fileItem.ProcessingJob?.Id, fileItem.CreatedAt, fileItem.UpdatedAt));
        });

        group.MapPatch("/files/{fileId:guid}/move", async (
            Guid fileId,
            MoveFileRequest request,
            ClaimsPrincipal user,
            AmanahDriveDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var fileItem = await dbContext.FileItems.SingleOrDefaultAsync(file => file.Id == fileId && file.UserId == userId, cancellationToken);
            if (fileItem is null)
            {
                return Results.NotFound();
            }

            if (request.FolderId is not null && !await FolderBelongsToUserAsync(dbContext, request.FolderId.Value, userId.Value, cancellationToken))
            {
                return Results.NotFound();
            }

            if (await FileNameExistsAsync(dbContext, userId.Value, request.FolderId, fileItem.OriginalFileName, cancellationToken, fileItem.Id))
            {
                return Results.Conflict(new ErrorResponse("A file with that name already exists in the destination folder."));
            }

            fileItem.FolderId = request.FolderId;
            fileItem.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new FileItemResponse(fileItem.Id, fileItem.FolderId, fileItem.OriginalFileName, fileItem.ContentType, fileItem.SizeBytes, fileItem.ChecksumSha256, fileItem.ProcessingJob?.Id, fileItem.CreatedAt, fileItem.UpdatedAt));
        });

        group.MapDelete("/files/{fileId:guid}", async (
            Guid fileId,
            ClaimsPrincipal user,
            AmanahDriveDbContext dbContext,
            IFileStorage storage,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var fileItem = await dbContext.FileItems.SingleOrDefaultAsync(file => file.Id == fileId && file.UserId == userId, cancellationToken);
            if (fileItem is null)
            {
                return Results.NotFound();
            }

            await storage.DeleteAsync(fileItem.StorageKey, cancellationToken);
            dbContext.FileItems.Remove(fileItem);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        });

        return group;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static IResult? ValidateRequest<TRequest>(TRequest request)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(request!);
        if (Validator.TryValidateObject(request!, context, validationResults, validateAllProperties: true))
        {
            return null;
        }

        return Results.ValidationProblem(validationResults.ToDictionary(
            result => result.MemberNames.FirstOrDefault() ?? string.Empty,
            result => new[] { result.ErrorMessage ?? "Invalid value." }));
    }

    private static IResult? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest(new ErrorResponse("Name is required."));
        }

        var trimmed = name.Trim();
        if (trimmed is "." or ".." ||
            trimmed.Contains("..", StringComparison.Ordinal) ||
            trimmed.IndexOfAny(['/', '\\']) >= 0 ||
            Path.GetFileName(trimmed) != trimmed ||
            trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return Results.BadRequest(new ErrorResponse("Name contains invalid path characters."));
        }

        return null;
    }

    private static async Task<bool> FolderBelongsToUserAsync(AmanahDriveDbContext dbContext, Guid folderId, Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Folders.AnyAsync(folder => folder.Id == folderId && folder.UserId == userId, cancellationToken);

    private static async Task<bool> FolderNameExistsAsync(AmanahDriveDbContext dbContext, Guid userId, Guid? parentFolderId, string name, CancellationToken cancellationToken, Guid? excludingFolderId = null) =>
        await dbContext.Folders.AnyAsync(
            folder =>
                folder.UserId == userId &&
                folder.ParentFolderId == parentFolderId &&
                folder.Name == name.Trim() &&
                folder.Id != excludingFolderId,
            cancellationToken);

    private static async Task<bool> FileNameExistsAsync(AmanahDriveDbContext dbContext, Guid userId, Guid? folderId, string fileName, CancellationToken cancellationToken, Guid? excludingFileId = null) =>
        await dbContext.FileItems.AnyAsync(
            file =>
                file.UserId == userId &&
                file.FolderId == folderId &&
                file.OriginalFileName == fileName.Trim() &&
                file.Id != excludingFileId,
            cancellationToken);

    private static async Task<HashSet<Guid>> GetDescendantFolderIdsAsync(AmanahDriveDbContext dbContext, Guid userId, Guid folderId, CancellationToken cancellationToken)
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

public sealed record CreateFolderRequest(
    [Required, MaxLength(255)] string Name,
    Guid? ParentFolderId);

public sealed record RenameRequest(
    [Required, MaxLength(255)] string Name);

public sealed record MoveFileRequest(Guid? FolderId);

public sealed record FolderResponse(Guid Id, string Name, Guid? ParentFolderId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record FileItemResponse(Guid Id, Guid? FolderId, string OriginalFileName, string ContentType, long SizeBytes, string ChecksumSha256, Guid? ProcessingJobId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record FolderContentsResponse(Guid? ParentFolderId, int Page, int PageSize, IReadOnlyCollection<FolderResponse> Folders, IReadOnlyCollection<FileItemResponse> Files);
