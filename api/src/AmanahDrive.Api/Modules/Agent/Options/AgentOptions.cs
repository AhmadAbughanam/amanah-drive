using System.ComponentModel.DataAnnotations;

namespace AmanahDrive.Api.Modules.Agent.Options;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    [Range(1, 10)]
    public int MaxIterations { get; init; } = 8;
}
