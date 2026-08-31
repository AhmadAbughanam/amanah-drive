namespace AmanahDrive.Api.Modules.AgentTools;

public interface IAgentTool<in TRequest, TResult>
{
    string Name { get; }

    bool RequiresApproval { get; }

    Task<AgentToolResult<TResult>> ExecuteAsync(
        AgentToolContext context,
        TRequest request,
        CancellationToken cancellationToken);
}

public sealed record AgentToolContext(Guid UserId);

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
