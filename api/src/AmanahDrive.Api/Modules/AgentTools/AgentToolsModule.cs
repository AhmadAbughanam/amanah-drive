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
        services.AddScoped<IAgentToolInvoker, AgentToolInvoker<ListFolderRequest, ListFolderResponse>>();
        services.AddScoped<IAgentToolInvoker, AgentToolInvoker<SearchFilesRequest, SearchFilesResponse>>();
        services.AddScoped<IAgentToolInvoker, AgentToolInvoker<ReadFileTextRequest, ReadFileTextResponse>>();
        services.AddScoped<IAgentToolInvoker, AgentToolInvoker<CreateFolderToolRequest, CreateFolderToolResponse>>();
        services.AddScoped<IAgentToolInvoker, AgentToolInvoker<CopyFileToolRequest, CopyFileToolResponse>>();
        services.AddScoped<IAgentToolInvoker, AgentToolInvoker<RenameFolderToolRequest, RenameFolderToolResponse>>();
        services.AddScoped<IAgentToolInvoker, AgentToolInvoker<RenameFileToolRequest, RenameFileToolResponse>>();
        services.AddScoped<IAgentToolInvoker, AgentToolInvoker<MoveFileToolRequest, MoveFileToolResponse>>();
        services.AddScoped<IAgentToolRegistry, AgentToolRegistry>();
        return services;
    }
}
