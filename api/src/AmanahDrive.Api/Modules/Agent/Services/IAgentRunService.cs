using AmanahDrive.Api.Modules.Agent.Models;

namespace AmanahDrive.Api.Modules.Agent.Services;

public interface IAgentRunService
{
    Task<AgentRun> StartAsync(Guid userId, string question, CancellationToken cancellationToken);

    Task<AgentRun?> GetAsync(Guid userId, Guid runId, CancellationToken cancellationToken);

    Task<AgentRun?> ApproveAsync(Guid userId, Guid runId, CancellationToken cancellationToken);

    Task<AgentRun?> RejectAsync(Guid userId, Guid runId, CancellationToken cancellationToken);
}
