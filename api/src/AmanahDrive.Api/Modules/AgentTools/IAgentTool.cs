namespace AmanahDrive.Api.Modules.AgentTools;

public interface IAgentTool<in TRequest, TResult>
{
    string Name { get; }

    AgentToolMetadata Metadata => AgentToolMetadataCatalog.For(Name);

    bool RequiresApproval { get; }

    Task<AgentToolResult<TResult>> ExecuteAsync(
        AgentToolContext context,
        TRequest request,
        CancellationToken cancellationToken);
}

public sealed record AgentToolContext(Guid UserId);

public sealed record AgentToolMetadata(string Name, string Description, System.Text.Json.JsonElement Parameters);

public static class AgentToolMetadataCatalog
{
    public static AgentToolMetadata For(string name) => name switch
    {
        "list_folder" => Create(name, "List folders and files at a folder location.", "{\"type\":\"object\",\"properties\":{\"parentFolderId\":{\"type\":[\"string\",\"null\"],\"format\":\"uuid\"},\"page\":{\"type\":\"integer\"},\"pageSize\":{\"type\":\"integer\"}}}"),
        "search_files" => Create(name, "Search the user's processed files semantically.", "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"},\"topK\":{\"type\":\"integer\"}},\"required\":[\"query\"]}"),
        "read_file_text" => Create(name, "Read extracted text from a file.", "{\"type\":\"object\",\"properties\":{\"fileId\":{\"type\":\"string\",\"format\":\"uuid\"}},\"required\":[\"fileId\"]}"),
        "create_folder" => Create(name, "Create a folder.", "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"parentFolderId\":{\"type\":[\"string\",\"null\"],\"format\":\"uuid\"}},\"required\":[\"name\"]}"),
        "copy_file" => Create(name, "Copy a file to a destination with a new name.", "{\"type\":\"object\",\"properties\":{\"sourceFileId\":{\"type\":\"string\",\"format\":\"uuid\"},\"destinationFolderId\":{\"type\":[\"string\",\"null\"],\"format\":\"uuid\"},\"name\":{\"type\":\"string\"}},\"required\":[\"sourceFileId\",\"name\"]}"),
        "rename_folder" => Create(name, "Rename a folder.", "{\"type\":\"object\",\"properties\":{\"folderId\":{\"type\":\"string\",\"format\":\"uuid\"},\"name\":{\"type\":\"string\"}},\"required\":[\"folderId\",\"name\"]}"),
        "rename_file" => Create(name, "Rename a file.", "{\"type\":\"object\",\"properties\":{\"fileId\":{\"type\":\"string\",\"format\":\"uuid\"},\"name\":{\"type\":\"string\"}},\"required\":[\"fileId\",\"name\"]}"),
        "move_file" => Create(name, "Move a file to a folder or the root.", "{\"type\":\"object\",\"properties\":{\"fileId\":{\"type\":\"string\",\"format\":\"uuid\"},\"destinationFolderId\":{\"type\":[\"string\",\"null\"],\"format\":\"uuid\"}},\"required\":[\"fileId\"]}"),
        _ => throw new InvalidOperationException($"No metadata is registered for agent tool '{name}'.")
    };

    private static AgentToolMetadata Create(string name, string description, string parameters) =>
        new(name, description, System.Text.Json.JsonDocument.Parse(parameters).RootElement.Clone());
}

public enum AgentToolStatus
{
    Success,
    NotFound,
    Conflict,
    Invalid
}

public sealed record AgentToolResult<TResult>(AgentToolStatus Status, TResult? Value = default, string? ErrorMessage = null)
{
    public static AgentToolResult<TResult> Success(TResult value) => new(AgentToolStatus.Success, value);

    public static AgentToolResult<TResult> NotFound() => new(AgentToolStatus.NotFound);

    public static AgentToolResult<TResult> Conflict(string errorMessage) => new(AgentToolStatus.Conflict, default, errorMessage);

    public static AgentToolResult<TResult> Invalid(string errorMessage) => new(AgentToolStatus.Invalid, default, errorMessage);
}
