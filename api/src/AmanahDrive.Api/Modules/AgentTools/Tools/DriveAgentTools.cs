using System.Text;
using AmanahDrive.Api.Modules.Drive.Endpoints;
using AmanahDrive.Api.Modules.Drive.Services;
using AmanahDrive.Api.Modules.SearchChat.Options;
using AmanahDrive.Api.Modules.SearchChat.Search;
using AmanahDrive.Api.Shared.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmanahDrive.Api.Modules.AgentTools.Tools;

public sealed record ListFolderRequest(Guid? ParentFolderId, int? Page = null, int? PageSize = null);

public sealed record ListFolderResponse(FolderContentsResponse Contents);

public sealed class ListFolderTool(IDriveService driveService) : IAgentTool<ListFolderRequest, ListFolderResponse>
{
    public string Name => "list_folder";

    public bool RequiresApproval => false;

    public async Task<AgentToolResult<ListFolderResponse>> ExecuteAsync(AgentToolContext context, ListFolderRequest request, CancellationToken cancellationToken)
    {
        var result = await driveService.ListFolderContentsAsync(context.UserId, request.ParentFolderId, request.Page, request.PageSize, cancellationToken);
        return DriveToolResultMapper.ToAgentResult(result, contents => new ListFolderResponse(contents));
    }
}

public sealed record SearchFilesRequest(string Query, int? TopK = null);

public sealed record SearchFilesResponse(IReadOnlyCollection<SearchFilesMatch> Results);

public sealed record SearchFilesMatch(Guid ChunkId, Guid FileId, string FileName, int ChunkIndex, string Snippet, double Score);

public sealed class SearchFilesTool(ISemanticSearchService searchService, IOptions<SearchOptions> options) : IAgentTool<SearchFilesRequest, SearchFilesResponse>
{
    public string Name => "search_files";

    public bool RequiresApproval => false;

    public async Task<AgentToolResult<SearchFilesResponse>> ExecuteAsync(AgentToolContext context, SearchFilesRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return AgentToolResult<SearchFilesResponse>.Invalid("Query is required.");
        }

        var topK = request.TopK is null
            ? options.Value.TopK
            : Math.Clamp(request.TopK.Value, 1, 25);
        var chunks = await searchService.SearchAsync(context.UserId, request.Query.Trim(), topK, cancellationToken);
        var results = chunks
            .Select(chunk => new SearchFilesMatch(
                chunk.ChunkId,
                chunk.FileId,
                chunk.FileName,
                chunk.ChunkIndex,
                CreateSnippet(chunk.Text, options.Value.SnippetLength),
                chunk.Score))
            .ToList();

        return AgentToolResult<SearchFilesResponse>.Success(new SearchFilesResponse(results));
    }

    private static string CreateSnippet(string text, int maxLength)
    {
        var normalized = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..(maxLength - 3)].TrimEnd() + "...";
    }
}

public sealed record ReadFileTextRequest(Guid FileId);

public sealed record ReadFileTextResponse(
    Guid FileId,
    string FileName,
    string Text,
    bool HasExtractedText,
    bool IsTruncated,
    int TotalCharacterCount,
    int ReturnedCharacterCount);

public sealed class ReadFileTextTool(AmanahDriveDbContext dbContext) : IAgentTool<ReadFileTextRequest, ReadFileTextResponse>
{
    private const int MaximumReturnedCharacters = 100_000;

    public string Name => "read_file_text";

    public bool RequiresApproval => false;

    public async Task<AgentToolResult<ReadFileTextResponse>> ExecuteAsync(AgentToolContext context, ReadFileTextRequest request, CancellationToken cancellationToken)
    {
        var file = await dbContext.FileItems
            .AsNoTracking()
            .Where(fileItem => fileItem.Id == request.FileId && fileItem.UserId == context.UserId)
            .Select(fileItem => new { fileItem.Id, fileItem.OriginalFileName })
            .SingleOrDefaultAsync(cancellationToken);
        if (file is null)
        {
            return AgentToolResult<ReadFileTextResponse>.NotFound();
        }

        var chunks = await dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.FileItemId == file.Id)
            .OrderBy(chunk => chunk.ChunkIndex)
            .Select(chunk => chunk.Text)
            .ToListAsync(cancellationToken);

        var text = new StringBuilder(Math.Min(MaximumReturnedCharacters, chunks.Sum(chunk => chunk.Length)));
        var totalCharacterCount = 0;
        foreach (var chunk in chunks)
        {
            totalCharacterCount += chunk.Length;
            var remaining = MaximumReturnedCharacters - text.Length;
            if (remaining > 0)
            {
                text.Append(chunk.AsSpan(0, Math.Min(remaining, chunk.Length)));
            }
        }

        var returnedText = text.ToString();
        return AgentToolResult<ReadFileTextResponse>.Success(new ReadFileTextResponse(
            file.Id,
            file.OriginalFileName,
            returnedText,
            chunks.Count > 0,
            totalCharacterCount > MaximumReturnedCharacters,
            totalCharacterCount,
            returnedText.Length));
    }
}

public sealed record CreateFolderToolRequest(string Name, Guid? ParentFolderId);

public sealed record CreateFolderToolResponse(FolderResponse Folder);

public sealed class CreateFolderTool(IDriveService driveService) : IAgentTool<CreateFolderToolRequest, CreateFolderToolResponse>
{
    public string Name => "create_folder";

    public bool RequiresApproval => false;

    public async Task<AgentToolResult<CreateFolderToolResponse>> ExecuteAsync(AgentToolContext context, CreateFolderToolRequest request, CancellationToken cancellationToken)
    {
        var result = await driveService.CreateFolderAsync(context.UserId, new CreateFolderRequest(request.Name, request.ParentFolderId), cancellationToken);
        return DriveToolResultMapper.ToAgentResult(result, folder => new CreateFolderToolResponse(folder));
    }
}

public sealed record CopyFileToolRequest(Guid SourceFileId, Guid? DestinationFolderId, string Name);

public sealed record CopyFileToolResponse(FileItemResponse File);

public sealed class CopyFileTool(IDriveService driveService) : IAgentTool<CopyFileToolRequest, CopyFileToolResponse>
{
    public string Name => "copy_file";

    public bool RequiresApproval => false;

    public async Task<AgentToolResult<CopyFileToolResponse>> ExecuteAsync(AgentToolContext context, CopyFileToolRequest request, CancellationToken cancellationToken)
    {
        var result = await driveService.CopyFileAsync(
            context.UserId,
            new CopyFileCommand(request.SourceFileId, request.DestinationFolderId, request.Name),
            cancellationToken);
        return DriveToolResultMapper.ToAgentResult(result, file => new CopyFileToolResponse(file));
    }
}

public sealed record RenameFolderToolRequest(Guid FolderId, string Name);

public sealed record RenameFolderToolResponse(FolderResponse Folder);

public sealed class RenameFolderTool(IDriveService driveService) : IAgentTool<RenameFolderToolRequest, RenameFolderToolResponse>
{
    public string Name => "rename_folder";

    public bool RequiresApproval => true;

    public async Task<AgentToolResult<RenameFolderToolResponse>> ExecuteAsync(AgentToolContext context, RenameFolderToolRequest request, CancellationToken cancellationToken)
    {
        var result = await driveService.RenameFolderAsync(context.UserId, request.FolderId, request.Name, cancellationToken);
        return DriveToolResultMapper.ToAgentResult(result, folder => new RenameFolderToolResponse(folder));
    }
}

public sealed record RenameFileToolRequest(Guid FileId, string Name);

public sealed record RenameFileToolResponse(FileItemResponse File);

public sealed class RenameFileTool(IDriveService driveService) : IAgentTool<RenameFileToolRequest, RenameFileToolResponse>
{
    public string Name => "rename_file";

    public bool RequiresApproval => true;

    public async Task<AgentToolResult<RenameFileToolResponse>> ExecuteAsync(AgentToolContext context, RenameFileToolRequest request, CancellationToken cancellationToken)
    {
        var result = await driveService.RenameFileAsync(context.UserId, request.FileId, request.Name, cancellationToken);
        return DriveToolResultMapper.ToAgentResult(result, file => new RenameFileToolResponse(file));
    }
}

public sealed record MoveFileToolRequest(Guid FileId, Guid? DestinationFolderId);

public sealed record MoveFileToolResponse(FileItemResponse File);

public sealed class MoveFileTool(IDriveService driveService) : IAgentTool<MoveFileToolRequest, MoveFileToolResponse>
{
    public string Name => "move_file";

    public bool RequiresApproval => true;

    public async Task<AgentToolResult<MoveFileToolResponse>> ExecuteAsync(AgentToolContext context, MoveFileToolRequest request, CancellationToken cancellationToken)
    {
        var result = await driveService.MoveFileAsync(context.UserId, request.FileId, request.DestinationFolderId, cancellationToken);
        return DriveToolResultMapper.ToAgentResult(result, file => new MoveFileToolResponse(file));
    }
}

internal static class DriveToolResultMapper
{
    public static AgentToolResult<TResult> ToAgentResult<TSource, TResult>(DriveOperationResult<TSource> result, Func<TSource, TResult> success)
    {
        return result.Status switch
        {
            DriveOperationStatus.Success when result.Value is not null => AgentToolResult<TResult>.Success(success(result.Value)),
            DriveOperationStatus.NotFound => AgentToolResult<TResult>.NotFound(),
            DriveOperationStatus.Conflict => AgentToolResult<TResult>.Conflict(result.ErrorMessage!),
            DriveOperationStatus.Invalid => AgentToolResult<TResult>.Invalid(result.ErrorMessage!),
            _ => throw new InvalidOperationException("Drive service returned an unsupported tool result.")
        };
    }
}
