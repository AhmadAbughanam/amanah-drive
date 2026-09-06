using System.ComponentModel.DataAnnotations;

namespace AmanahDrive.Api.Modules.Agent.Options;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    [Range(1, 10)]
    public int MaxIterations { get; init; } = 8;

    public bool WorkerEnabled { get; init; } = true;

    [Range(1, 60)]
    public int WorkerPollSeconds { get; init; } = 1;
}
