namespace AmanahDrive.Api.Modules.Agent.Models;

public sealed class AgentRun
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string Question { get; set; }

    public AgentRunStatus Status { get; set; }

    public string? FinalAnswer { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<AgentRunStep> Steps { get; set; } = [];
}

public enum AgentRunStatus
{
    AwaitingApproval,
    Completed,
    IterationLimitReached,
    Failed
}
