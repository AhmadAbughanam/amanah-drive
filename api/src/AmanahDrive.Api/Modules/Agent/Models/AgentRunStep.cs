namespace AmanahDrive.Api.Modules.Agent.Models;

public sealed class AgentRunStep
{
    public Guid Id { get; set; }

    public Guid AgentRunId { get; set; }

    public AgentRun AgentRun { get; set; } = null!;

    public int Sequence { get; set; }

    public required string Role { get; set; }

    public string? Content { get; set; }

    public string? ToolCallId { get; set; }

    public string? ToolName { get; set; }

    public string? ToolArgumentsJson { get; set; }

    public AgentToolCallStatus? ToolCallStatus { get; set; }

    public bool RequiresApproval { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public enum AgentToolCallStatus
{
    PendingApproval,
    Executing,
    Executed,
    Rejected,
    Invalid
}
