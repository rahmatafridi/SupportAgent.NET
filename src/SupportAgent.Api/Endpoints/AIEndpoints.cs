using System.Text.Json;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.AI;
using SupportAgent.Infrastructure.AI.Tools;

namespace SupportAgent.Api.Endpoints;

/// <summary>
/// Request body for the AI chat endpoint.
/// </summary>
/// <param name="Message">The user's message to send to the AI assistant.</param>
public record ChatRequest(string Message);

/// <summary>
/// Describes one tool that the LLM chose to call during a chat request.
/// </summary>
/// <param name="Name">The tool name selected by the model, such as GetCustomer.</param>
/// <param name="Arguments">The arguments the model passed to the tool.</param>
public record ToolUsageResponse(string Name, JsonElement Arguments);

/// <summary>
/// One grounded knowledge source cited in the AI response.
/// </summary>
/// <param name="Title">Knowledge document title.</param>
/// <param name="Source">Optional document source label.</param>
public record KnowledgeSourceResponse(string Title, string? Source);

/// <summary>
/// Response returned by the AI chat endpoint.
/// </summary>
/// <param name="Text">The assistant's final natural-language answer.</param>
/// <param name="Provider">The AI provider that handled the request, such as Ollama or OpenAI.</param>
/// <param name="Model">The model name used for the request.</param>
/// <param name="TotalTokens">Total tokens consumed across all model calls in the request.</param>
/// <param name="DurationMs">Total duration in milliseconds.</param>
/// <param name="ToolsUsed">Tools the LLM chose to call while answering the request.</param>
/// <param name="Sources">Grounded knowledge sources cited from SearchKnowledgeBase results.</param>
public record ChatResponse(
    string Text,
    string Provider,
    string Model,
    int TotalTokens,
    long DurationMs,
    IReadOnlyList<ToolUsageResponse> ToolsUsed,
    IReadOnlyList<KnowledgeSourceResponse> Sources);

/// <summary>
/// AI chat endpoints. These call <see cref="IAIGateway"/> and never talk to Ollama or OpenAI directly.
/// </summary>
public static class AIEndpoints
{
    /// <summary>
    /// Maps AI-related routes to the application pipeline.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same route builder so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapAIEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/ai/chat
        // Sends the user's message to the AI gateway with support tools enabled.
        // The LLM decides whether to call GetCustomer, GetOrderStatus, or SearchKnowledgeBase.
        app.MapPost("/api/ai/chat", async (
            ChatRequest request,
            IAIGateway aiGateway,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { error = "Message is required." });
            }

            var aiRequest = new AIRequest
            {
                SystemPrompt = SupportAgentSystemPrompts.SupportAssistant,
                Prompt = request.Message,
                Tools = SupportToolDefinitions.GetAll()
            };

            try
            {
                var response = await aiGateway.GenerateAsync(aiRequest, cancellationToken);

                return Results.Ok(new ChatResponse(
                    response.Text ?? string.Empty,
                    response.Provider,
                    response.Model,
                    response.TotalTokens,
                    (long)response.Duration.TotalMilliseconds,
                    MapToolsUsed(response.ToolsUsed),
                    MapSources(response.Sources)));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        })
        .WithName("PostAIChat")
        .WithSummary("Sends a chat message to the AI assistant.")
        .WithDescription("Uses native LLM tool calling through IAIGateway. The model may call GetCustomer, GetOrderStatus, or SearchKnowledgeBase before returning the final answer.");

        return app;
    }

    /// <summary>
    /// Converts internal tool call records into API-friendly response objects.
    /// </summary>
    private static IReadOnlyList<ToolUsageResponse> MapToolsUsed(IReadOnlyList<Core.Models.AI.AIToolCall> toolsUsed) =>
        toolsUsed
            .Select(toolCall => new ToolUsageResponse(
                toolCall.Name,
                ParseArguments(toolCall.ArgumentsJson)))
            .ToList();

    /// <summary>
    /// Converts grounded knowledge sources into API-friendly response objects.
    /// </summary>
    private static IReadOnlyList<KnowledgeSourceResponse> MapSources(IReadOnlyList<KnowledgeSource> sources) =>
        sources
            .Select(source => new KnowledgeSourceResponse(source.Title, source.Source))
            .ToList();

    /// <summary>
    /// Parses tool arguments JSON so it can be returned as structured JSON in the API response.
    /// </summary>
    private static JsonElement ParseArguments(string argumentsJson)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        return document.RootElement.Clone();
    }
}
