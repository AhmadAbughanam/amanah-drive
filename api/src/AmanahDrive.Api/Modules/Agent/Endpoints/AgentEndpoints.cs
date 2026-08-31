using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
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
        var steps = run.Steps
            .Where(step => step.Role != "system")
            .OrderBy(step => step.Sequence)
            .Select(ToStepResponse)
            .ToList();
        return new AgentRunResponse(
            run.Id,
            run.Status.ToString(),
            run.FinalAnswer,
            run.FailureReason,
            pending?.ToolName,
            pending is null ? null : DescribeToolArguments(pending),
            steps,
            run.CreatedAt,
            run.UpdatedAt);
    }

    private static AgentRunStepResponse ToStepResponse(AgentRunStep step) => new(
        step.Sequence,
        step.Role,
        step.Role is "user" or "assistant" ? step.Content : null,
        step.ToolName,
        step.ToolName is null ? null : DescribeToolArguments(step),
        step.ToolName is null ? null : DescribeToolResult(step),
        step.ToolCallStatus?.ToString(),
        step.RequiresApproval,
        step.CreatedAt);

    private static string DescribeToolArguments(AgentRunStep step)
    {
        using var document = TryParseJson(step.ToolArgumentsJson);
        var arguments = document?.RootElement;
        var name = ReadString(arguments, "name");
        var query = ReadString(arguments, "query");
        var parentFolderId = ReadString(arguments, "parentFolderId");
        var destinationFolderId = ReadString(arguments, "destinationFolderId");

        return step.ToolName switch
        {
            "list_folder" => parentFolderId is null ? "List items in the root folder" : "List items in a folder",
            "search_files" => string.IsNullOrWhiteSpace(query) ? "Search files" : $"Search files for “{query}”",
            "read_file_text" => "Read extracted text from a file",
            "create_folder" => string.IsNullOrWhiteSpace(name) ? "Create a folder" : $"Create folder “{name}”",
            "copy_file" => string.IsNullOrWhiteSpace(name) ? "Copy a file" : $"Copy a file as “{name}”",
            "rename_folder" => string.IsNullOrWhiteSpace(name) ? "Rename a folder" : $"Rename a folder to “{name}”",
            "rename_file" => string.IsNullOrWhiteSpace(name) ? "Rename a file" : $"Rename a file to “{name}”",
            "move_file" => destinationFolderId is null ? "Move a file to the root folder" : "Move a file to a folder",
            _ => "Run an agent tool"
        };
    }

    private static string? DescribeToolResult(AgentRunStep step)
    {
        if (step.ToolCallStatus == AgentToolCallStatus.PendingApproval) return "Waiting for your approval.";
        if (step.ToolCallStatus == AgentToolCallStatus.Executing) return "Working…";
        if (step.ToolCallStatus == AgentToolCallStatus.Rejected) return "You rejected this action.";
        if (string.IsNullOrWhiteSpace(step.Content)) return null;

        using var document = TryParseJson(step.Content);
        var result = document?.RootElement;
        var status = ReadString(result, "status");
        var error = ReadString(result, "errorMessage");
        if (!string.IsNullOrWhiteSpace(error)) return Truncate(error, 280);
        return status switch
        {
            "success" or "Success" => "Completed.",
            "notFound" or "NotFound" => "No matching item was found.",
            "conflict" or "Conflict" => "The action could not be completed because of a conflict.",
            "invalid" or "Invalid" => "The requested action was invalid.",
            _ => "Completed."
        };
    }

    private static JsonDocument? TryParseJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return JsonDocument.Parse(value); }
        catch (JsonException) { return null; }
    }

    private static string? ReadString(JsonElement? element, string propertyName) =>
        element is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(propertyName, out var property)
            ? property.ValueKind == JsonValueKind.String ? property.GetString() : null
            : null;

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : $"{value[..(maximumLength - 1)]}…";

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}

public sealed record StartAgentRunRequest([Required, MaxLength(4000)] string Question);

public sealed record AgentRunResponse(
    Guid Id,
    string Status,
    string? FinalAnswer,
    string? FailureReason,
    string? PendingToolName,
    string? PendingActionSummary,
    IReadOnlyList<AgentRunStepResponse> Steps,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AgentRunStepResponse(
    int Sequence,
    string Role,
    string? Content,
    string? ToolName,
    string? ArgumentsSummary,
    string? ResultSummary,
    string? Status,
    bool RequiresApproval,
    DateTimeOffset CreatedAt);
