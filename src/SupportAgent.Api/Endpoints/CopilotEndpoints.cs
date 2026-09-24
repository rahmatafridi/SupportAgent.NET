using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Antiforgery;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;
using SupportAgent.Infrastructure.AI.Tools;
using SupportAgent.Api.Authorization;
using SupportAgent.Api.Security;

namespace SupportAgent.Api.Endpoints;

/// <summary>
/// Request body for POST /api/copilot/ask.
/// </summary>
public record CopilotAskRequest(
    int TicketId,
    Guid? ConversationId,
    string Message);

/// <summary>
/// Response body for POST /api/copilot/ask.
/// </summary>
public record CopilotAskResponse(
    Guid ConversationId,
    string Answer,
    IReadOnlyList<ToolUsageResponse> ToolsUsed,
    IReadOnlyList<KnowledgeSourceResponse> Sources);

/// <summary>
/// Request body for POST /api/copilot/draft-reply.
/// </summary>
public record CopilotDraftRequest(int TicketId);

/// <summary>
/// Structured suggested reply returned by POST /api/copilot/draft-reply.
/// </summary>
public record CopilotDraftResponseBody(
    string Subject,
    string Body,
    string Tone,
    double Confidence);

/// <summary>
/// Response body for POST /api/copilot/draft-reply.
/// </summary>
public record CopilotDraftResponse(
    CopilotDraftResponseBody Draft,
    IReadOnlyList<ToolUsageResponse> ToolsUsed,
    IReadOnlyList<KnowledgeSourceResponse> Sources);

/// <summary>
/// Ticket-aware AI copilot endpoints for support agents.
/// </summary>
public static class CopilotEndpoints
{
    /// <summary>
    /// Maps copilot routes to the application pipeline.
    /// </summary>
    public static IEndpointRouteBuilder MapCopilotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/copilot/ask", async (
            CopilotAskRequest request,
            ICopilotService copilotService,
            IAIUsageService usageService,
            CancellationToken cancellationToken) =>
        {
            if (request.TicketId <= 0)
            {
                return Results.BadRequest(new { error = "TicketId is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { error = "Message is required." });
            }

            try
            {
                var result = await copilotService.AskAsync(
                    request.TicketId,
                    request.ConversationId,
                    request.Message,
                    cancellationToken);
                await usageService.RecordUsageAsync(result.Usage, "CopilotAsk", cancellationToken);

                return Results.Ok(new CopilotAskResponse(
                    result.ConversationId,
                    result.Answer,
                    MapToolsUsed(result.ToolsUsed),
                    MapSources(result.Sources)));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        })
        .WithName("PostCopilotAsk")
        .RequireAuthorization(AuthorizationPolicies.AiAccess)
        .RequireRateLimiting("ai")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithSummary("Asks the AI copilot a ticket-aware support question.")
        .WithDescription("Loads ticket context, persists conversation state, and allows native tool calling.");

        app.MapPost("/api/copilot/draft-reply", async (
            CopilotDraftRequest request,
            ICopilotService copilotService,
            IAIUsageService usageService,
            CancellationToken cancellationToken) =>
        {
            if (request.TicketId <= 0)
            {
                return Results.BadRequest(new { error = "TicketId is required." });
            }

            try
            {
                var result = await copilotService.DraftReplyAsync(request.TicketId, cancellationToken);
                await usageService.RecordUsageAsync(result.Usage, "DraftReply", cancellationToken);

                return Results.Ok(new CopilotDraftResponse(
                    new CopilotDraftResponseBody(
                        result.Draft.Subject,
                        result.Draft.Body,
                        result.Draft.Tone,
                        result.Draft.Confidence),
                    MapToolsUsed(result.ToolsUsed),
                    MapSources(result.Sources)));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        })
        .WithName("PostCopilotDraftReply")
        .RequireAuthorization(AuthorizationPolicies.AiAccess)
        .RequireRateLimiting("ai")
        .AddEndpointFilter<AntiforgeryEndpointFilter>()
        .WithSummary("Generates a suggested customer reply draft for human review.")
        .WithDescription("Does not send customer replies automatically.");

        return app;
    }

    private static IReadOnlyList<ToolUsageResponse> MapToolsUsed(IReadOnlyList<AIToolCall> toolsUsed) =>
        toolsUsed
            .Select(toolCall => new ToolUsageResponse(
                toolCall.Name,
                ParseArguments(toolCall.ArgumentsJson)))
            .ToList();

    private static IReadOnlyList<KnowledgeSourceResponse> MapSources(IReadOnlyList<KnowledgeSource> sources) =>
        sources
            .Select(source => new KnowledgeSourceResponse(source.Title, source.Source))
            .ToList();

    private static JsonElement ParseArguments(string argumentsJson)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        return document.RootElement.Clone();
    }
}
