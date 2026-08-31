using System.Text.Json;

namespace AmanahDrive.Api.Modules.AgentTools;

public interface IAgentToolRegistry
{
    IReadOnlyCollection<AgentToolMetadata> Tools { get; }

    bool TryGet(string name, out IAgentToolInvoker tool);
}

public interface IAgentToolInvoker
{
    AgentToolMetadata Metadata { get; }

    bool RequiresApproval { get; }

    Task<AgentToolInvocationResult> InvokeAsync(AgentToolContext context, string argumentsJson, CancellationToken cancellationToken);
}

public sealed record AgentToolInvocationResult(AgentToolStatus Status, string ResultJson, string? ErrorMessage = null);

public sealed class AgentToolRegistry(IEnumerable<IAgentToolInvoker> tools) : IAgentToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAgentToolInvoker> _byName = tools.ToDictionary(tool => tool.Metadata.Name, StringComparer.Ordinal);

    public IReadOnlyCollection<AgentToolMetadata> Tools => _byName.Values.Select(tool => tool.Metadata).ToList();

    public bool TryGet(string name, out IAgentToolInvoker tool) => _byName.TryGetValue(name, out tool!);
}

public sealed class AgentToolInvoker<TRequest, TResult>(IAgentTool<TRequest, TResult> tool) : IAgentToolInvoker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AgentToolMetadata Metadata => tool.Metadata;

    public bool RequiresApproval => tool.RequiresApproval;

    public async Task<AgentToolInvocationResult> InvokeAsync(AgentToolContext context, string argumentsJson, CancellationToken cancellationToken)
    {
        TRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<TRequest>(argumentsJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            return Invalid($"Tool arguments were invalid JSON: {exception.Message}");
        }

        if (request is null)
        {
            return Invalid("Tool arguments must be a JSON object.");
        }

        var result = await tool.ExecuteAsync(context, request, cancellationToken);
        return new AgentToolInvocationResult(result.Status, JsonSerializer.Serialize(result, JsonOptions), result.ErrorMessage);
    }

    private static AgentToolInvocationResult Invalid(string error) =>
        new(AgentToolStatus.Invalid, JsonSerializer.Serialize(new { status = AgentToolStatus.Invalid, errorMessage = error }, JsonOptions), error);
}
