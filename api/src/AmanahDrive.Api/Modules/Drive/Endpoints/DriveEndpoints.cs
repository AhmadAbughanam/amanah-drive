using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AmanahDrive.Api.Modules.Drive.Services;
using AmanahDrive.Api.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Mvc;

namespace AmanahDrive.Api.Modules.Drive.Endpoints;

public static class DriveEndpoints
{
    public static RouteGroupBuilder MapDriveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/drive").RequireAuthorization();
        group.WithTags("Drive");

        group.MapGet("/folders", async (
            Guid? parentFolderId,
            int? page,
            int? pageSize,
            ClaimsPrincipal user,
            IDriveService driveService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await driveService.ListFolderContentsAsync(userId.Value, parentFolderId, page, pageSize, cancellationToken);
            return ToResult(result, Results.Ok);
        })
            .WithSummary("List folders and files in a folder.")
            .Produces<FolderContentsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/folders", async (
            CreateFolderRequest request,
            ClaimsPrincipal user,
            IDriveService driveService,
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

            var result = await driveService.CreateFolderAsync(userId.Value, request, cancellationToken);
            return ToResult(result, folder => Results.Created($"/drive/folders/{folder.Id}", folder));
        })
            .WithSummary("Create a folder.")
            .Produces<FolderResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapPatch("/folders/{folderId:guid}/rename", async (
            Guid folderId,
            RenameRequest request,
            ClaimsPrincipal user,
            IDriveService driveService,
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

            var result = await driveService.RenameFolderAsync(userId.Value, folderId, request.Name, cancellationToken);
            return ToResult(result, Results.Ok);
        })
            .WithSummary("Rename a folder.")
            .Produces<FolderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapDelete("/folders/{folderId:guid}", async (
            Guid folderId,
            ClaimsPrincipal user,
            IDriveService driveService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await driveService.DeleteFolderAsync(userId.Value, folderId, cancellationToken);
            return ToResult(result, Results.NoContent);
        })
            .WithSummary("Delete a folder and its descendants.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/files/upload", async (
            [FromForm] IFormFile file,
            [FromForm] Guid? folderId,
            ClaimsPrincipal user,
            IDriveService driveService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            await using var stream = file.OpenReadStream();
            var result = await driveService.UploadFileAsync(
                userId.Value,
                new UploadFileCommand(file.FileName, file.ContentType, file.Length, folderId, stream),
                cancellationToken);
            return ToResult(result, fileItem => Results.Created($"/drive/files/{fileItem.Id}", fileItem));
        })
            .DisableAntiforgery()
            .WithSummary("Upload a file and create a processing job.")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<FileItemResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status413PayloadTooLarge)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/youtube", async (
            AddYouTubeRequest request,
            ClaimsPrincipal user,
            IDriveService driveService,
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

            var result = await driveService.AddYouTubeAsync(userId.Value, new AddYouTubeCommand(request.Url, request.FolderId), cancellationToken);
            return ToResult(result, fileItem => Results.Created($"/drive/files/{fileItem.Id}", fileItem));
        })
            .WithSummary("Add a YouTube video and create a transcript processing job.")
            .Produces<FileItemResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        group.MapGet("/files/{fileId:guid}/download", async (
            Guid fileId,
            ClaimsPrincipal user,
            IDriveService driveService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await driveService.OpenFileReadAsync(userId.Value, fileId, cancellationToken);
            return result.RedirectUrl is not null
                ? Results.Redirect(result.RedirectUrl)
                : result.File is null
                    ? Results.NotFound()
                    : Results.File(result.File.Content, result.File.ContentType, result.File.FileName);
        })
            .WithSummary("Download a stored file.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/files/{fileId:guid}/rename", async (
            Guid fileId,
            RenameRequest request,
            ClaimsPrincipal user,
            IDriveService driveService,
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

            var result = await driveService.RenameFileAsync(userId.Value, fileId, request.Name, cancellationToken);
            return ToResult(result, Results.Ok);
        })
            .WithSummary("Rename a file.")
            .Produces<FileItemResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        group.MapPatch("/files/{fileId:guid}/move", async (
            Guid fileId,
            MoveFileRequest request,
            ClaimsPrincipal user,
            IDriveService driveService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await driveService.MoveFileAsync(userId.Value, fileId, request.FolderId, cancellationToken);
            return ToResult(result, Results.Ok);
        })
            .WithSummary("Move a file to another folder or the root.")
            .Produces<FileItemResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        group.MapDelete("/files/{fileId:guid}", async (
            Guid fileId,
            ClaimsPrincipal user,
            IDriveService driveService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await driveService.DeleteFileAsync(userId.Value, fileId, cancellationToken);
            return ToResult(result, Results.NoContent);
        })
            .WithSummary("Delete a file, processing job, and chunks.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static IResult ToResult<T>(DriveOperationResult<T> result, Func<T, IResult> success)
    {
        return result.Status switch
        {
            DriveOperationStatus.Success when result.Value is not null => success(result.Value),
            DriveOperationStatus.NotFound => Results.NotFound(),
            DriveOperationStatus.Conflict => Results.Conflict(new ErrorResponse(result.ErrorMessage!)),
            DriveOperationStatus.PayloadTooLarge => Results.StatusCode(StatusCodes.Status413PayloadTooLarge),
            DriveOperationStatus.Invalid => Results.BadRequest(new ErrorResponse(result.ErrorMessage!)),
            _ => throw new InvalidOperationException("Drive service returned an invalid operation result.")
        };
    }

    private static IResult ToResult(DriveOperationResult result, Func<IResult> success)
    {
        return result.Status switch
        {
            DriveOperationStatus.Success => success(),
            DriveOperationStatus.NotFound => Results.NotFound(),
            _ => throw new InvalidOperationException("Drive service returned an invalid operation result.")
        };
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
}

public sealed record CreateFolderRequest(
    [Required, MaxLength(255)] string Name,
    Guid? ParentFolderId);

public sealed record RenameRequest(
    [Required, MaxLength(255)] string Name);

public sealed record MoveFileRequest(Guid? FolderId);

public sealed record AddYouTubeRequest(
    [Required, MaxLength(2048)] string Url,
    Guid? FolderId);

public sealed record FolderResponse(Guid Id, string Name, Guid? ParentFolderId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record FileItemResponse(Guid Id, Guid? FolderId, string OriginalFileName, string ContentType, long SizeBytes, string ChecksumSha256, string Source, string? SourceUrl, Guid? ProcessingJobId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record FolderContentsResponse(Guid? ParentFolderId, int Page, int PageSize, IReadOnlyCollection<FolderResponse> Folders, IReadOnlyCollection<FileItemResponse> Files);
