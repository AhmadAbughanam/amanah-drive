using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AmanahDrive.Api.Modules.Agent.Models;
using AmanahDrive.Api.Modules.Agent.Services;
using AmanahDrive.Api.Shared.Infrastructure.Ai;
using AmanahDrive.Api.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Mvc;

namespace AmanahDrive.Api.Modules.Agent.Endpoints;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/agent/runs").RequireAuthorization().RequireRateLimiting("ai").WithTags("Agent");

        group.MapPost("", async (StartAgentRunRequest request, ClaimsPrincipal user, IAgentRunService service, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 4000)
            {
                return Results.BadRequest(new ErrorResponse("Question is required and must be at most 4000 characters."));
            }

            var userId = GetUserId(user);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                var run = await service.StartAsync(userId.Value, request.Question, cancellationToken);
                return Results.Created($"/agent/runs/{run.Id}", ToResponse(run));
            }
            catch (AiServiceException)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        }).Produces<AgentRunResponse>(StatusCodes.Status201Created);

        group.MapPost("/{runId:guid}/approve", async (Guid runId, ClaimsPrincipal user, IAgentRunService service, CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();
            try
            {
                var run = await service.ApproveAsync(userId.Value, runId, cancellationToken);
                return run is null ? Results.NotFound() : Results.Ok(ToResponse(run));
            }
            catch (AiServiceException)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        }).Produces<AgentRunResponse>(StatusCodes.Status200OK);

        group.MapPost("/{runId:guid}/reject", async (Guid runId, ClaimsPrincipal user, IAgentRunService service, CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();
            try
            {
                var run = await service.RejectAsync(userId.Value, runId, cancellationToken);
                return run is null ? Results.NotFound() : Results.Ok(ToResponse(run));
            }
            catch (AiServiceException)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        }).Produces<AgentRunResponse>(StatusCodes.Status200OK);

        group.MapGet("/{runId:guid}", async (Guid runId, ClaimsPrincipal user, IAgentRunService service, CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();
            var run = await service.GetAsync(userId.Value, runId, cancellationToken);
            return run is null ? Results.NotFound() : Results.Ok(ToResponse(run));
        }).Produces<AgentRunResponse>(StatusCodes.Status200OK);

        return app;
    }

    private static AgentRunResponse ToResponse(AgentRun run)
    {
        var pending = run.Steps.SingleOrDefault(step => step.ToolCallStatus == AgentToolCallStatus.PendingApproval);
        return new AgentRunResponse(run.Id, run.Status.ToString(), run.FinalAnswer, run.FailureReason, pending?.ToolName, pending?.ToolArgumentsJson, run.CreatedAt, run.UpdatedAt);
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}

public sealed record StartAgentRunRequest([Required, MaxLength(4000)] string Question);

public sealed record AgentRunResponse(Guid Id, string Status, string? FinalAnswer, string? FailureReason, string? PendingToolName, string? PendingToolArgumentsJson, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
