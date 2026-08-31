using AmanahDrive.Api.Modules.AgentTools.Tools;

namespace AmanahDrive.Api.Modules.AgentTools;

public static class AgentToolsModule
{
    public static IServiceCollection AddAgentToolsModule(this IServiceCollection services)
    {
        services.AddScoped<IAgentTool<ListFolderRequest, ListFolderResponse>, ListFolderTool>();
        services.AddScoped<IAgentTool<SearchFilesRequest, SearchFilesResponse>, SearchFilesTool>();
        services.AddScoped<IAgentTool<ReadFileTextRequest, ReadFileTextResponse>, ReadFileTextTool>();
        services.AddScoped<IAgentTool<CreateFolderToolRequest, CreateFolderToolResponse>, CreateFolderTool>();
        services.AddScoped<IAgentTool<CopyFileToolRequest, CopyFileToolResponse>, CopyFileTool>();
        services.AddScoped<IAgentTool<RenameFolderToolRequest, RenameFolderToolResponse>, RenameFolderTool>();
        services.AddScoped<IAgentTool<RenameFileToolRequest, RenameFileToolResponse>, RenameFileTool>();
        services.AddScoped<IAgentTool<MoveFileToolRequest, MoveFileToolResponse>, MoveFileTool>();
        return services;
    }
}
